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
using VisualProfiler.Patches;

namespace VisualProfiler;

public class Plugin : TorchPluginBase, IWpfPlugin
{
    internal static readonly Logger Log = LogManager.GetLogger("VisualProfiler");

    public static Plugin Instance => instance;
    static Plugin instance = null!;

    //Persistent<ConfigViewModel> configVM = null!;

    internal const int MaxRecordingSeconds = 60;
    internal const int MaxRecordingFrames = 3600;

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

    internal static async Task<ProfilerEventsRecording> LoadRecordingAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ProfilerEventsRecording recording;
        byte[] fileBytes;

#if NET
        fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
#else
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
        {
            fileBytes = new byte[fs.Length];
            await fs.ReadAsync(fileBytes, 0, fileBytes.Length, cancellationToken).ConfigureAwait(false);
        }
#endif

        recording = await Task.Run(() =>
        {
            using var stream = new MemoryStream(fileBytes);
            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);

            return Serializer.Deserialize<ProfilerEventsRecording>(gzipStream);
        }, cancellationToken).ConfigureAwait(false);

        return recording;
    }

    static byte[] SerializeRecording(ProfilerEventsRecording recording)
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

    internal static async Task<byte[]> PrepareAndSerializeRecordingAsync(ProfilerEventsRecording recording)
    {
        ProfilerHelper.PrepareRecordingForSerialization(recording);

        // Run on thread pool
        var serializedRecording = await Task.Run(() => SerializeRecording(recording)).ConfigureAwait(false);

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
        var serializedRecording = await PrepareAndSerializeRecordingAsync(recording).ConfigureAwait(false);

        if (filePath == null)
            return (false, serializedRecording);

        try
        {
            FileStream stream;
#if NET
            await using ((stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)).ConfigureAwait(false))
                await stream.WriteAsync(serializedRecording, CancellationToken.None).ConfigureAwait(false);
#else
            using (stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                await stream.WriteAsync(serializedRecording, 0, serializedRecording.Length, CancellationToken.None).ConfigureAwait(false);
#endif

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
        var fileName = recording.SessionName.Replace(' ', '_').TrimEnd('.');

        foreach (var item in Path.GetInvalidPathChars())
            fileName = fileName.Replace(item, '_');

        fileName += $"--{recording.StartTime:yyyy-MM-dd--HH-mm-ss}--F{recording.NumFrames}";

        return fileName;
    }

    internal static void SendRecordingToClient(byte[] serializedRecording, string fileName, CommandContext context)
    {
        const ushort FileNetMessageId = 19911; // Picked at random, may conflict with other mods/plugins.

        var filePayload = new FilePayload { FileName = fileName, RecordingFile = serializedRecording };
        byte[] messagePayload;

        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, filePayload);
            messagePayload = stream.ToArray();
        }

        // TODO: Large file streaming

        Log.Info($"Sending profiler recording ({messagePayload.Length:N} bytes) to client '{context.Player.DisplayName}', UserID: {context.Player.SteamUserId}");

        int nBytes = messagePayload.Length;

        double value;
        string unit;

        if (nBytes > 1024 * 1024)
        {
            value = nBytes / (double)(1024 * 1024);
            unit = "MB";
        }
        else if (nBytes > 1024)
        {
            value = nBytes / 1024.0;
            unit = "KB";
        }
        else
        {
            value = nBytes;
            unit = "B";
        }

        context.Respond($"Sending recording payload of {value:N2} {unit}.");

        MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(FileNetMessageId, messagePayload, context.Player.SteamUserId, reliable: true);
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
        string? keepObjects = null;
        string? profileClusters = null;
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
            case "keepobjects":
                if (argValue == null)
                {
                    Context.Respond("Command error: Missing value for --keepobjects argument.");
                    return;
                }

                keepObjects = argValue;
                break;
            case "profileclusters":
                if (argValue == null)
                {
                    Context.Respond("Command error: Missing value for --profileclusters argument.");
                    return;
                }

                profileClusters = argValue;
                break;
            case "savetofile":
                saveToFile = true;
                break;
            case "sendtoclient":
                if (Context.Player == null)
                {
                    Context.Respond("Command error: --sendtoclient argument is not valid for the server, it can only by used by clients.");
                    return;
                }

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

            StartSeconds(secs, keepObjects, profileClusters, saveToFile, sendToClient);
            return;
        }

        if (frames != null)
        {
            StartFrames(frames, keepObjects, profileClusters, saveToFile, sendToClient);
            return;
        }
    }

    void StartSeconds(string seconds, string? keepObjects, string? profileClusters, bool saveToFile, bool sendToClient)
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

        if (numSeconds > Plugin.MaxRecordingSeconds)
        {
            Context.Respond($"Command Error: Profiler recording is limited to a maximum of {Plugin.MaxRecordingSeconds} seconds.");
            return;
        }

        if (Profiler.IsRecordingEvents)
        {
            Context.Respond("Command Error: Cannot start profiling, a profiler recording has already been started.");
            return;
        }

        if (keepObjects != null)
        {
            if (!bool.TryParse(keepObjects, out bool bKeepObjects))
            {
                Context.Respond("Command Error: Invalid argument boolean value.");
                return;
            }

            Profiler.SetObjectRecordingEnabled(bKeepObjects);
            ProfilerWindow.Instance?.Dispatcher?.BeginInvoke(() => ProfilerWindow.Instance.OnPropertyChanged(nameof(ProfilerWindow.RecordEventObjects)));
        }

        if (profileClusters != null)
        {
            if (!bool.TryParse(profileClusters, out bool bProfileClusters))
            {
                Context.Respond("Command Error: Invalid argument boolean value.");
                return;
            }

            MyPhysics_Patches.ProfileEachCluster = bProfileClusters;
            ProfilerWindow.Instance?.Dispatcher?.BeginInvoke(() => ProfilerWindow.Instance.OnPropertyChanged(nameof(ProfilerWindow.ProfilePhysicsClusters)));
        }

        var sessionName = Plugin.Instance.Torch.CurrentSession?.KeenSession?.Name ?? "";

        Plugin.Log.Info($"Starting a profiler recording for {numSeconds} seconds.");

        Profiler.StartEventRecording();

        Timer timer = null!;

        timer = new Timer(TimerCompleted, null, TimeSpan.FromSeconds(numSeconds), Timeout.InfiniteTimeSpan);

        Context.Respond($"Started a profiler recording for {numSeconds} seconds.");

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
                await SummarizeSaveSendAsync(recording, saveToFile, sendToClient);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Unhandled exception while saving/sending profiler recording summary.");
            }
        }
    }

    void StartFrames(string frames, string? keepObjects, string? profileClusters, bool saveToFile, bool sendToClient)
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

        if (numFrames > Plugin.MaxRecordingFrames)
        {
            Context.Respond($"Command Error: Profiler recording is limited to a maximum of {Plugin.MaxRecordingFrames} frames.");
            return;
        }

        if (Profiler.IsRecordingEvents)
        {
            Context.Respond("Command Error: Cannot start profiling, a profiler recording has already been started.");
            return;
        }

        if (keepObjects != null)
        {
            if (!bool.TryParse(keepObjects, out bool bKeepObjects))
            {
                Context.Respond("Command Error: Invalid argument boolean value.");
                return;
            }

            Profiler.SetObjectRecordingEnabled(bKeepObjects);
            ProfilerWindow.Instance?.Dispatcher?.BeginInvoke(() => ProfilerWindow.Instance.OnPropertyChanged(nameof(ProfilerWindow.RecordEventObjects)));
        }

        if (profileClusters != null)
        {
            if (!bool.TryParse(profileClusters, out bool bProfileClusters))
            {
                Context.Respond("Command Error: Invalid argument boolean value.");
                return;
            }

            MyPhysics_Patches.ProfileEachCluster = bProfileClusters;
            ProfilerWindow.Instance?.Dispatcher?.BeginInvoke(() => ProfilerWindow.Instance.OnPropertyChanged(nameof(ProfilerWindow.ProfilePhysicsClusters)));
        }

        var sessionName = Plugin.Instance.Torch.CurrentSession?.KeenSession?.Name ?? "";

        Plugin.Log.Info($"Starting a profiler recording for {numFrames} frames with a timeout length of {Plugin.MaxRecordingSeconds} seconds.");

        Timer? timer = null;

        Profiler.StartEventRecording(numFrames, OnRecordingCompleted);

        timer = new Timer(TimerCompleted, null, TimeSpan.FromSeconds(Plugin.MaxRecordingSeconds), Timeout.InfiniteTimeSpan);

        Context.Respond($"Started a profiler recording for {numFrames} frames.");

        void TimerCompleted(object? state)
        {
            if (!Profiler.IsRecordingEvents)
                return;

            var recording = Profiler.StopEventRecording();

            if (state != null)
                Plugin.Log.Info("Frame profiling was timed out.");

            ClearTimer();
            OnRecordingCompleted(recording);
        }

        void ClearTimer()
        {
            if (timer == null)
                return;

            timer.Change(Timeout.Infinite, Timeout.Infinite); // Cancel timer
            timer.Dispose();
            timer = null;
        }

        async void OnRecordingCompleted(ProfilerEventsRecording recording)
        {
            ClearTimer();

            recording.SessionName = sessionName;

            try
            {
                await SummarizeSaveSendAsync(recording, saveToFile, sendToClient);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Unhandled exception while saving/sending profiler recording summary.");
            }
        }
    }

    async Task SummarizeSaveSendAsync(ProfilerEventsRecording recording, bool saveToFile, bool sendToClient)
    {
        var summary = ProfilerHelper.SummarizeRecording(recording);

        if (!string.IsNullOrEmpty(summary))
            Context.Torch.Invoke(() => Context.Respond(summary));

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
            if (serializedRecording == null)
            {
                Context.Torch.Invoke(() => Context.Respond("Serializing recording for transfer."));

                serializedRecording = await Plugin.PrepareAndSerializeRecordingAsync(recording).ConfigureAwait(false);
            }

            var fileName = Plugin.GetRecordingFileName(recording);

            Context.Torch.Invoke(() => Plugin.SendRecordingToClient(serializedRecording, fileName, Context));
        }
    }
}
