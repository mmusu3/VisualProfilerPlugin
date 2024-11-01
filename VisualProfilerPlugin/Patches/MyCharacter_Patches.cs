using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Character;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyCharacter_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyCharacter).GetNonPublicInstanceMethod("RigidBody_ContactPointCallback");
        var prefix = typeof(MyCharacter_Patches).GetNonPublicStaticMethod(nameof(Prefix_RigidBody_ContactPointCallback));
        var suffix = typeof(MyCharacter_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey RigidBody_ContactPointCallback;

        internal static void Init()
        {
            RigidBody_ContactPointCallback = ProfilerKeyCache.GetOrAdd("MyCharacter.RigidBody_ContactPointCallback");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.RigidBody_ContactPointCallback, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
