using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyWheel_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyWheel).GetPublicInstanceMethod(nameof(MyWheel.ContactPointCallback));
        var prefix = typeof(MyWheel_Patches).GetNonPublicStaticMethod(nameof(Prefix_ContactPointCallback));
        var suffix = typeof(MyWheel_Patches).GetNonPublicStaticMethod(nameof(Suffix_ContactPointCallback));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ContactPointCallback(ref ProfilerTimer __local_timer, MyWheel __instance)
    {
        __local_timer = Profiler.Start("MyWheel.ContactPointCallback", profileMemory: true,
            new(__instance, "Wheel: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ContactPointCallback(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
