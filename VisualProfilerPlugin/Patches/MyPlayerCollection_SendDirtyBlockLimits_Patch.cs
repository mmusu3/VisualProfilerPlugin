using System.Runtime.CompilerServices;
using Sandbox.Game.Multiplayer;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyPlayerCollection_SendDirtyBlockLimits_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyPlayerCollection).GetPublicInstanceMethod(nameof(MyPlayerCollection.SendDirtyBlockLimits));
        var prefix = typeof(MyPlayerCollection_SendDirtyBlockLimits_Patch).GetNonPublicStaticMethod(nameof(Prefix_SendDirtyBlockLimits));
        var suffix = typeof(MyPlayerCollection_SendDirtyBlockLimits_Patch).GetNonPublicStaticMethod(nameof(Suffix_SendDirtyBlockLimits));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SendDirtyBlockLimits(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPlayerCollection.SendDirtyBlockLimits");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SendDirtyBlockLimits(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
