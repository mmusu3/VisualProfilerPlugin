using System.Runtime.CompilerServices;
using Sandbox.Game.Screens.Helpers;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyAsyncSaving_Start_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyAsyncSaving).GetPublicStaticMethod(nameof(MyAsyncSaving.Start));
        var prefix = typeof(MyAsyncSaving_Start_Patch).GetNonPublicStaticMethod(nameof(Prefix_Start));
        var suffix = typeof(MyAsyncSaving_Start_Patch).GetNonPublicStaticMethod(nameof(Suffix_Start));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Start(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyAsyncSaving.Start");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Start(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
