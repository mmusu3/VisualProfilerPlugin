using System.Runtime.CompilerServices;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyPhysicsBody_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var suffix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var source = typeof(MyPhysicsBody).GetNonPublicInstanceMethod("OnContactPointCallback");
        var prefix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnContactPointCallback));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyPhysicsBody).GetNonPublicInstanceMethod("OnContactSoundCallback");
        prefix = typeof(MyPhysicsBody_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnContactSoundCallback));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey OnContactPointCallback;
        internal static ProfilerKey OnContactSoundCallback;

        internal static void Init()
        {
            OnContactPointCallback = ProfilerKeyCache.GetOrAdd("MyPhysicsBody.OnContactPointCallback");
            OnContactSoundCallback = ProfilerKeyCache.GetOrAdd("MyPhysicsBody.OnContactSoundCallback");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnContactPointCallback(ref ProfilerTimer __local_timer, MyPhysicsBody __instance)
    {
        __local_timer = Profiler.Start(Keys.OnContactPointCallback, ProfilerTimerOptions.ProfileMemory,
            new(__instance.Entity, "PhysicsBody entity: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnContactSoundCallback(ref ProfilerTimer __local_timer, MyPhysicsBody __instance)
    {
        __local_timer = Profiler.Start(Keys.OnContactSoundCallback, ProfilerTimerOptions.ProfileMemory,
            new(__instance.Entity, "PhysicsBody entity: {0}"));

        return true;
    }
}
