using System.Runtime.CompilerServices;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyNetworkWriter_SendAll_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyNetworkWriter).GetPublicStaticMethod(nameof(MyNetworkWriter.SendAll));
        var prefix = typeof(MyNetworkWriter_SendAll_Patch).GetNonPublicStaticMethod(nameof(Prefix_SendAll));
        var suffix = typeof(MyNetworkWriter_SendAll_Patch).GetNonPublicStaticMethod(nameof(Suffix_SendAll));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SendAll(ref ProfilerTimer __local_timer)
    {
        // TODO: Need to add packet count data to profiler events
        __local_timer = Profiler.Start("MyNetworkWriter.SendAll");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SendAll(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
