using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyWheel_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyWheel).GetPublicInstanceMethod(nameof(MyWheel.ContactPointCallback));
        var prefix = typeof(MyWheel_Patches).GetNonPublicStaticMethod(nameof(Prefix_ContactPointCallback));
        var suffix = typeof(MyWheel_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey ContactPointCallback;

        internal static void Init()
        {
            ContactPointCallback = ProfilerKeyCache.GetOrAdd("MyWheel.ContactPointCallback");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ContactPointCallback(ref ProfilerTimer __local_timer, MyWheel __instance)
    {
        __local_timer = Profiler.Start(Keys.ContactPointCallback, ProfilerTimerOptions.ProfileMemory,
            new(__instance, "Wheel: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
