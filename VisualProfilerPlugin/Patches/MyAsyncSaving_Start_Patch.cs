using System.Runtime.CompilerServices;
using Sandbox.Game.Screens.Helpers;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyAsyncSaving_Start_Patch
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyAsyncSaving).GetPublicStaticMethod(nameof(MyAsyncSaving.Start));
        var prefix = typeof(MyAsyncSaving_Start_Patch).GetNonPublicStaticMethod(nameof(Prefix_Start));
        var suffix = typeof(MyAsyncSaving_Start_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Start;

        internal static void Init()
        {
            Start = ProfilerKeyCache.GetOrAdd("MyAsyncSaving.Start");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Start(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Start); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
