using System.Runtime.CompilerServices;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyNetworkMonitor_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyNetworkMonitor).GetNonPublicStaticMethod("UpdateInternal");
        var prefix = typeof(MyNetworkMonitor_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateInternal));
        var suffix = typeof(MyNetworkMonitor_Patches).GetNonPublicStaticMethod(nameof(Suffix_UpdateInternal));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static bool isInitialized;

    static void Init()
    {
        Profiler.SetSortingGroupForCurrentThread("Network");
        Profiler.SetSortingGroupOrderPriority("Network", 1);

        isInitialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateInternal(ref ProfilerTimer __local_timer)
    {
        if (!isInitialized)
            Init();

        __local_timer = Profiler.Start("MyNetworkMonitor.UpdateInternal");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateInternal(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
