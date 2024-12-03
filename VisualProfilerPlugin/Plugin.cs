using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Win32;
using NLog;
using ProtoBuf;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.Commands;
using Torch.Commands.Permissions;

namespace VisualProfiler;

public class Plugin : TorchPluginBase, IWpfPlugin
{
    internal static readonly Logger Log = LogManager.GetLogger("VisualProfiler");

    public static Plugin Instance => instance;
    static Plugin instance = null!;

    //Persistent<ConfigViewModel> configVM = null!;

    public override void Init(ITorchBase torch)
    {
        base.Init(torch);

        instance = this;

        Log.Info("Loading config");

        //var configPath = Path.Combine(StoragePath, "VisualProfiler", "config.xml");
        //configVM = Persistent<ConfigViewModel>.Load(configPath);

        Profiler.SetEventObjectResolver(ProfilerHelper.ProfilerEventObjectResolver);

#if NETFRAMEWORK
        Patches.Torch_MethodContext_Patches.Patch();
#endif
        Patches.PrioritizedScheduler_Patches.Patch();
    }

    public UserControl GetControl() => new ConfigView(new()/*configVM.Data*/);

    internal static ProfilerEventsRecording LoadRecording(string filePath)
    {
        ProfilerEventsRecording recording;

        using (var stream = File.Open(filePath, FileMode.Open))
        {
            using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                recording = Serializer.Deserialize<ProfilerEventsRecording>(gzipStream);
        }

        return recording;
    }

    internal static byte[] SerializeRecording(ProfilerEventsRecording recording)
    {
        ProfilerHelper.PrepareRecordingForSerialization(recording);

        return SerializeRecordingImpl(recording);
    }

    static byte[] SerializeRecordingImpl(ProfilerEventsRecording recording)
    {
        byte[] serializedRecording;

        using (var stream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(stream, CompressionLevel.Optimal))
                Serializer.Serialize(gzipStream, recording);

            serializedRecording = stream.ToArray();
        }

        return serializedRecording;
    }

    internal static async Task<byte[]> SerializeRecordingAsync(ProfilerEventsRecording recording)
    {
        ProfilerHelper.PrepareRecordingForSerialization(recording);

        // Run on thread pool
        var serializedRecording = await Task.Run(() => SerializeRecordingImpl(recording)).ConfigureAwait(false);

        return serializedRecording;
    }

    internal static Task<(bool Written, byte[] SerializedRecording)> SaveRecordingAsync(ProfilerEventsRecording recording)
    {
        return SaveRecordingAsync(recording, out _);
    }

    internal static Task<(bool Written, byte[] SerializedRecording)> SaveRecordingAsync(ProfilerEventsRecording recording, out string filePath)
    {
        var folderPath = Path.Combine(Instance.StoragePath, "VisualProfiler", "Recordings");

        Directory.CreateDirectory(folderPath);

        var sessionName = GetRecordingFileName(recording);

        filePath = Path.Combine(folderPath, sessionName) + ".prec";

        return SaveRecordingAsync(recording, filePath);
    }

    internal static async Task<bool> SaveRecordingDialogAsync(ProfilerEventsRecording recording)
    {
        var folderPath = Path.Combine(Instance.StoragePath, "VisualProfiler", "Recordings");

        Directory.CreateDirectory(folderPath);

        var sessionName = GetRecordingFileName(recording);

        var diag = new SaveFileDialog {
            InitialDirectory = folderPath,
            FileName = sessionName,
            DefaultExt = ".prec",
            Filter = "Profiler Recordings (.prec)|*.prec"
        };

        bool? result = diag.ShowDialog();

        if (result != true)
            return false;

        var filePath = diag.FileName;

        (bool written, _) = await SaveRecordingAsync(recording, filePath).ConfigureAwait(false);

        return written;
    }

    internal static async Task<(bool Written, byte[] SerializedRecording)> SaveRecordingAsync(ProfilerEventsRecording recording, string? filePath)
    {
        var serializedRecording = await SerializeRecordingAsync(recording).ConfigureAwait(false);

        if (filePath == null)
            return (false, serializedRecording);

        try
        {
            FileStream stream;
#if NET
            await using ((stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)).ConfigureAwait(false))
#else
            using (stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
#endif
                await stream.WriteAsync(serializedRecording, 0, serializedRecording.Length, CancellationToken.None).ConfigureAwait(false);

            Log.Info($"Saved profiler recording to \"{filePath}\".");

            return (true, serializedRecording);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to save profiler recording to \"{filePath}\".");

            return (false, serializedRecording);
        }
    }

    internal static string GetRecordingFileName(ProfilerEventsRecording recording)
    {
        var fileName = recording.SessionName.Replace(' ', '_');

        foreach (var item in Path.GetInvalidPathChars())
            fileName = fileName.Replace(item, '_');

        fileName += "-" + recording.StartTime.ToString("s").Replace(':', '-');

        return fileName;
    }

    internal static void SendRecordingToClient(byte[] serializedRecording, string fileName, ulong userId)
    {
        const ushort FileNetMessageId = 19911; // Picked at random, may conflict with other mods/plugins.

        var filePayload = new FilePayload { FileName = fileName, RecordingFile = serializedRecording };
        byte[] messagePayload;

        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, filePayload);
            messagePayload = stream.ToArray();
        }

        MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(FileNetMessageId, messagePayload, userId, reliable: true);
    }

    [ProtoContract]
    class FilePayload
    {
        [ProtoMember(1)]
        public string FileName;

        [ProtoMember(2)]
        public byte[] RecordingFile;

        public FilePayload()
        {
            FileName = null!;
            RecordingFile = null!;
        }
    }
}

public class ConfigViewModel : ViewModel
{
}

[Category("vprofiler")]
public class Commands : CommandModule
{
    [Command("start", "Starts a new profiler recording.")]
    [Permission(VRage.Game.ModAPI.MyPromoteLevel.Admin)]
    public void Start()
    {
        if (Context.Args.Count < 1)
        {
            Context.Respond("Command error: Must specify a number of either seconds or frames. (Eg. --secs=1 or --frames=10 )");
            return;
        }

        string? secs = null;
        string? frames = null;
        bool saveToFile = false;
        bool sendToClient = false;

        foreach (var item in Context.Args)
        {
            var arg = item;

            if (!arg.StartsWith("--"))
            {
                Context.Respond("Command error: Invalid argument format.");
                return;
            }

            arg = arg.Remove(0, "--".Length);

            int splitIndex = arg.IndexOf('=');

            string argType;
            string? argValue = null;

            if (splitIndex < 1)
            {
                argType = arg;
            }
            else
            {
                var parts = arg.Split(['='], StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2)
                {
                    Context.Respond("Command error: Missing argument value.");
                    return;
                }

                argType = parts[0];
                argValue = parts[1];
            }

            switch (argType)
            {
            case "secs":
                if (argValue == null)
                {
                    Context.Respond("Command error: Missing value for --secs argument.");
                    return;
                }

                secs = argValue;
                break;
            case "frames":
                if (argValue == null)
                {
                    Context.Respond("Command error: Missing value for --frames argument.");
                    return;
                }

                frames = argValue;
                break;
            case "savetofile":
                saveToFile = true;
                break;
            case "sendtoclient":
                sendToClient = true;
                break;
            default:
                Context.Respond($"Command error: Invalid argument: {item}.");
                return;
            }
        }

        if (secs != null)
        {
            if (frames != null)
            {
                Context.Respond("Command error: Must only specify one of --secs or --frames.");
                return;
            }

            StartSeconds(secs, saveToFile, sendToClient);
            return;
        }

        if (frames != null)
        {
            StartFrames(frames, saveToFile, sendToClient);
            return;
        }
    }

    void StartSeconds(string seconds, bool saveToFile, bool sendToClient)
    {
        double numSeconds;

        if (!double.TryParse(seconds, out numSeconds))
        {
            Context.Respond("Command Error: Invalid argument number value.");
            return;
        }

        if (numSeconds <= 0)
        {
            Context.Respond($"Command Error: Seconds argument must be greater than 0.");
            return;
        }

        const double maxSeconds = 60;

        if (numSeconds > maxSeconds)
        {
            Context.Respond($"Command Error: Profiler recording is limited to a maximum of {maxSeconds} seconds.");
            return;
        }

        if (Profiler.IsRecordingEvents)
        {
            Context.Respond("Command Error: Cannot start profiling, a profiler recording has already been started.");
            return;
        }

        var sessionName = Plugin.Instance.Torch.CurrentSession?.KeenSession?.Name ?? "";

        Context.Respond($"Started a profiler recording for {numSeconds} seconds.");

        Profiler.StartEventRecording();

        Timer timer = null!;

        timer = new Timer(TimerCompleted, null, TimeSpan.FromSeconds(numSeconds), Timeout.InfiniteTimeSpan);

        async void TimerCompleted(object? state)
        {
            if (timer != null)
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite); // Cancel timer
                timer.Dispose();
            }

            if (!Profiler.IsRecordingEvents)
                return;

            var recording = Profiler.StopEventRecording();

            recording.SessionName = sessionName;

            try
            {
                await SaveSendSummarizeAsync(recording, saveToFile, sendToClient);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Unhandled exception while saving/sending profiler recording summary.");
            }
        }
    }

    void StartFrames(string frames, bool saveToFile, bool sendToClient)
    {
        int numFrames;

        if (!int.TryParse(frames, out numFrames))
        {
            Context.Respond("Command Error: Invalid argument number value.");
            return;
        }

        if (numFrames <= 0)
        {
            Context.Respond($"Command Error: Frames argument must be greater than 0.");
            return;
        }

        const int maxFrames = 3600;

        if (numFrames > maxFrames)
        {
            Context.Respond($"Command Error: Profiler recording is limited to a maximum of {maxFrames} frames.");
            return;
        }

        if (Profiler.IsRecordingEvents)
        {
            Context.Respond("Command Error: Cannot start profiling, a profiler recording has already been started.");
            return;
        }

        var sessionName = Plugin.Instance.Torch.CurrentSession?.KeenSession?.Name ?? "";

        Profiler.StartEventRecording(numFrames, OnRecordingCompleted);

        Context.Respond($"Started a profiler recording for {numFrames} frames.");

        async void OnRecordingCompleted(ProfilerEventsRecording recording)
        {
            recording.SessionName = sessionName;

            try
            {
                await SaveSendSummarizeAsync(recording, saveToFile, sendToClient);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Unhandled exception while saving/sending profiler recording summary.");
            }
        }
    }

    async Task SaveSendSummarizeAsync(ProfilerEventsRecording recording, bool saveToFile, bool sendToClient)
    {
        byte[]? serializedRecording = null;

        if (saveToFile)
        {
            Context.Torch.Invoke(() => Context.Respond($"Saving profiler recording file."));

            (bool written, serializedRecording) = await Plugin.SaveRecordingAsync(recording, out var filePath).ConfigureAwait(false);
            var fileName = Path.GetFileName(filePath);

            Context.Torch.Invoke(() =>
            {
                var msg = written
                    ? $"Saved profiler recording file as \"{fileName}\"."
                    : $"Failed to save profiler recording to file.";

                Context.Respond(msg);
            });
        }

        if (sendToClient)
        {
            serializedRecording ??= await Plugin.SerializeRecordingAsync(recording).ConfigureAwait(false);

            var fileName = Plugin.GetRecordingFileName(recording);

            Context.Torch.Invoke(() => Plugin.SendRecordingToClient(serializedRecording, fileName, Context.Player.SteamUserId));
        }

        var summary = ProfilerHelper.SummarizeRecording(recording);

        if (!string.IsNullOrEmpty(summary))
            Context.Torch.Invoke(() => Context.Respond(summary));
    }
}
