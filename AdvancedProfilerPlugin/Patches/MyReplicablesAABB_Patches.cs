using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Replication;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyReplicablesAABB_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyReplicablesAABB).GetPublicInstanceMethod(nameof(MyReplicablesAABB.GetReplicablesInBox));
        var prefix = typeof(MyReplicablesAABB_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnEvent));
        var suffix = typeof(MyReplicablesAABB_Patches).GetNonPublicStaticMethod(nameof(Suffix_OnEvent));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnEvent(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicablesAABB.GetReplicablesInBox");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_OnEvent(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
