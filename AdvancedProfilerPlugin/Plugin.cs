using System.IO;
using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;

namespace AdvancedProfiler;

public class Plugin : TorchPluginBase, IWpfPlugin
{
    internal static readonly Logger Log = LogManager.GetLogger("AdvancedProfiler");

    static Plugin instance = null!;

    Persistent<ConfigViewModel> configVM = null!;

    public override void Init(ITorchBase torch)
    {
        base.Init(torch);

        instance = this;

        Log.Info("Loading config");

        var configPath = Path.Combine(StoragePath, "AdvancedProfiler.cfg");
        configVM = Persistent<ConfigViewModel>.Load(configPath);

        Patches.Worker_WorkerLoop_Patch.Patch();
    }

    public UserControl GetControl() => new ConfigView(configVM.Data);
}

public class ConfigViewModel : ViewModel
{
}
