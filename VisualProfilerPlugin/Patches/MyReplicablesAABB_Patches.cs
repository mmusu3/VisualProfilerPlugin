using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Replication;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyReplicablesAABB_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyReplicablesAABB).GetPublicInstanceMethod(nameof(MyReplicablesAABB.GetReplicablesInBox));
        var prefix = typeof(MyReplicablesAABB_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnEvent));
        var suffix = typeof(MyReplicablesAABB_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey GetReplicablesInBox;

        internal static void Init()
        {
            GetReplicablesInBox = ProfilerKeyCache.GetOrAdd("MyReplicablesAABB.GetReplicablesInBox");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnEvent(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.GetReplicablesInBox); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
