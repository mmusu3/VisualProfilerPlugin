using System.IO;
using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;

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
        Patches.Worker_WorkerLoop_Patch.Patch();
    }

    public UserControl GetControl() => new ConfigView(new()/*configVM.Data*/);
}

public class ConfigViewModel : ViewModel
{
}
