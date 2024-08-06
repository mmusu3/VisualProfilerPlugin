using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Character;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyCharacter_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyCharacter).GetNonPublicInstanceMethod("RigidBody_ContactPointCallback");
        var prefix = typeof(MyCharacter_Patches).GetNonPublicStaticMethod(nameof(Prefix_RigidBody_ContactPointCallback));
        var suffix = typeof(MyCharacter_Patches).GetNonPublicStaticMethod(nameof(Suffix_RigidBody_ContactPointCallback));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start("MyCharacter.RigidBody_ContactPointCallback", profileMemory: true, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
