using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Threading;
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

    internal static byte[] SerializeRecording(ProfilerEventsRecording recording)
    {
        ProfilerHelper.PrepareRecordingForSerialization(recording);

        byte[] serializedRecording;

        using (var stream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(stream, CompressionLevel.Optimal))
                Serializer.Serialize(gzipStream, recording);

            serializedRecording = stream.ToArray();
        }

        return serializedRecording;
    }

    // TODO: Async
    internal static bool SaveRecording(ProfilerEventsRecording recording)
    {
        return SaveRecording(recording, out _, out _);
    }

    // TODO: Async
    internal static bool SaveRecording(ProfilerEventsRecording recording, out byte[] serializedRecording, [NotNullWhen(true)] out string? filePath)
    {
        var folderPath = Path.Combine(Instance.StoragePath, "VisualProfiler", "Recordings");

        Directory.CreateDirectory(folderPath);

        var sessionName = GetRecordingFileName(recording);

        filePath = Path.Combine(folderPath, sessionName) + ".prec";

        return SaveRecording(recording, filePath, out serializedRecording);
    }

    // TODO: Async
    internal static bool SaveRecordingDialog(ProfilerEventsRecording recording)
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

        return SaveRecording(recording, filePath, out _);
    }

    // TODO: Async
    internal static bool SaveRecording(ProfilerEventsRecording recording, string? filePath, out byte[] serializedRecording)
    {
        // TODO: Do on dedicated thread
        serializedRecording = SerializeRecording(recording);

        if (filePath == null)
            return false;

        try
        {
            File.WriteAllBytes(filePath, serializedRecording);
            return true;
        }
        catch // TODO: Log
        {
            return false;
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

        void TimerCompleted(object? state)
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

            byte[]? serializedRecording = null;

            if (saveToFile)
            {
                // TODO: Handle return
                Plugin.SaveRecording(recording, out serializedRecording, out var filePath);

                Context.Torch.Invoke(() =>
                {
                    var msg = $"Saved profiler recording file as {Path.GetFileName(filePath)}.";

                    Context.Respond(msg);
                    Plugin.Log.Info(msg);
                });
            }

            if (sendToClient)
            {
                serializedRecording ??= Plugin.SerializeRecording(recording);

                var fileName = Plugin.GetRecordingFileName(recording);

                Context.Torch.Invoke(() =>
                {
                    Plugin.SendRecordingToClient(serializedRecording, fileName, Context.Player.SteamUserId);
                });
            }

            var summary = ProfilerHelper.SummarizeRecording(recording);

            if (!string.IsNullOrEmpty(summary))
                Context.Respond(summary);
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

        void OnRecordingCompleted(ProfilerEventsRecording recording)
        {
            recording.SessionName = sessionName;

            byte[]? serializedRecording = null;

            if (saveToFile)
            {
                // TODO: Handle return
                Plugin.SaveRecording(recording, out serializedRecording, out var filePath);

                var msg = $"Saved profiler recording file as {Path.GetFileName(filePath)}.";

                Context.Respond(msg);
                Plugin.Log.Info(msg);
            }

            if (sendToClient)
            {
                serializedRecording ??= Plugin.SerializeRecording(recording);

                var fileName = Plugin.GetRecordingFileName(recording);

                Context.Torch.Invoke(() =>
                {
                    Plugin.SendRecordingToClient(serializedRecording, fileName, Context.Player.SteamUserId);
                });
            }

            var summary = ProfilerHelper.SummarizeRecording(recording);

            if (!string.IsNullOrEmpty(summary))
                Context.Respond(summary);
        }
    }
}
