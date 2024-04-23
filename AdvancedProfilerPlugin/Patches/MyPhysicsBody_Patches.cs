using System.Runtime.CompilerServices;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyPhysicsBody_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyPhysicsBody).GetNonPublicInstanceMethod("OnContactPointCallback");
        var prefix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnContactPointCallback));
        var suffix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Suffix_OnContactPointCallback));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyPhysicsBody).GetNonPublicInstanceMethod("OnContactSoundCallback");
        prefix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnContactSoundCallback));
        suffix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Suffix_OnContactSoundCallback));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnContactPointCallback(ref ProfilerTimer __local_timer, MyPhysicsBody __instance)
    {
        __local_timer = Profiler.Start("MyPhysicsBody.OnContactPointCallback", profileMemory: true,
            new(__instance.Entity, "PhysicsBody entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_OnContactPointCallback(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnContactSoundCallback(ref ProfilerTimer __local_timer, MyPhysicsBody __instance)
    {
        __local_timer = Profiler.Start("MyPhysicsBody.OnContactSoundCallback", profileMemory: true,
            new(__instance.Entity, "PhysicsBody entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_OnContactSoundCallback(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
