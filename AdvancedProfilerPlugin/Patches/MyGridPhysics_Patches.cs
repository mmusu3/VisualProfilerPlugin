using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyGridPhysics_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyGridPhysics).GetNonPublicInstanceMethod("RigidBody_ContactPointCallback");
        var prefix = typeof(MyGridPhysics_Patches).GetNonPublicStaticMethod(nameof(Prefix_RigidBody_ContactPointCallback));
        var suffix = typeof(MyGridPhysics_Patches).GetNonPublicStaticMethod(nameof(Suffix_RigidBody_ContactPointCallback));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer, MyGridPhysics __instance)
    {
        __local_timer = Profiler.Start("MyGridPhysics.RigidBody_ContactPointCallback", profileMemory: true,
            new(__instance.Entity, "Grid entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
