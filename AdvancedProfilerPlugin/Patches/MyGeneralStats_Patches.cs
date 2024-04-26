using System.Runtime.CompilerServices;
using Sandbox.Engine;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyGeneralStats_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyGeneralStats).GetPublicInstanceMethod("Update");
        var prefix = typeof(MyGeneralStats_Patches).GetNonPublicStaticMethod(nameof(Prefix_Update));
        var suffix = typeof(MyGeneralStats_Patches).GetNonPublicStaticMethod(nameof(Suffix_Update));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Update(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyGeneralStats.Update");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Update(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
