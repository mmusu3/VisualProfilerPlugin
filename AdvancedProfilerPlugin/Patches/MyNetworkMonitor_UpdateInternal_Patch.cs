using System.Runtime.CompilerServices;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyNetworkMonitor_UpdateInternal_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyNetworkMonitor).GetNonPublicStaticMethod("UpdateInternal");
        var prefix = typeof(MyNetworkMonitor_UpdateInternal_Patch).GetNonPublicStaticMethod(nameof(Prefix));
        var suffix = typeof(MyNetworkMonitor_UpdateInternal_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyNetworkMonitor.UpdateInternal");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
