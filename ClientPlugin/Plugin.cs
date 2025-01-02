using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ParallelTasks;
using ProtoBuf;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Plugins;
using VRage.Utils;
using VRageRender;

namespace VisualProfilerClientPlugin;

public class Plugin : IPlugin
{
    const ushort MessageId = 19911; // Picked at random, may conflict with other mods/plugins.

    public void Init(object gameInstance)
    {
        MySession.OnLoading += MySession_OnLoading;
        MySession.OnUnloading += MySession_OnUnloading;
    }

    public void Dispose()
    {
        MySession.OnLoading -= MySession_OnLoading;
        MySession.OnUnloading -= MySession_OnUnloading;
    }

    void MySession_OnLoading()
    {
        MyAPIGateway.Multiplayer?.RegisterSecureMessageHandler(MessageId, MessageHandler);
    }

    void MySession_OnUnloading()
    {
        MyAPIGateway.Multiplayer?.UnregisterSecureMessageHandler(MessageId, MessageHandler);
    }

    public void Update() { }

    static void MessageHandler(ushort id, byte[] payload, ulong playerPlatformId, bool fromServer)
    {
        if (!fromServer)
        {
            MyLog.Default.Warning("VisualProfiler client plugin received network message not from server.");
            return;
        }

        FilePayload? filePayload;

        try
        {
            using (var stream = new MemoryStream(payload))
                filePayload = Serializer.Deserialize<FilePayload>(stream);
        }
        catch (Exception ex)
        {
            filePayload = null;

            MyLog.Default.Error("VProfiler Plugin: Exception while deserializing file payload.\r\n" + ex.ToString());
            MyAPIGateway.Utilities.ShowMessage("VProfiler", "Error while receiving recording file. See log for details.");
        }

        if (filePayload == null)
            return;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var recordingsFolder = Path.Combine(appData, "SpaceEngineers", "ProfilerRecordings");

        Directory.CreateDirectory(recordingsFolder);

        using var diag = new SaveFileDialog {
            Title = "Save Profiler Recording",
            InitialDirectory = recordingsFolder,
            DefaultExt = ".prec",
            Filter = "Profiler recording (*.prec)|*.prec",
            AddExtension = true,
            FileName = filePayload.FileName
        };

        //var mainWindow = (Form)MyRenderProxy.RenderThread.RenderWindow;

        var t = new Thread(() =>
        {
            var result = diag.ShowDialog(/*mainWindow*/);

            if (result is DialogResult.OK or DialogResult.Yes)
                Parallel.Start(WorkPriority.VeryLow, () => File.WriteAllBytes(diag.FileName, filePayload.RecordingFile), Parallel.DefaultOptions);
        });

        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
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
