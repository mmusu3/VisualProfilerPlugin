using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyPlanet_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyPlanet.UpdateOnceBeforeFrame), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyPlanet.UpdateAfterSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdatePlanetPhysics", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyPlanet.UpdateAfterSimulation100), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyPlanet).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyPlanet_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyPlanet_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey UpdateOnceBeforeFrame;
        internal static ProfilerKey UpdateAfterSimulation10;
        internal static ProfilerKey UpdatePlanetPhysics;
        internal static ProfilerKey UpdateAfterSimulation100;

        internal static void Init()
        {
            UpdateOnceBeforeFrame = ProfilerKeyCache.GetOrAdd("MyPlanet.UpdateOnceBeforeFrame");
            UpdateAfterSimulation10 = ProfilerKeyCache.GetOrAdd("MyPlanet.UpdateAfterSimulation10");
            UpdatePlanetPhysics = ProfilerKeyCache.GetOrAdd("MyPlanet.UpdatePlanetPhysics");
            UpdateAfterSimulation100 = ProfilerKeyCache.GetOrAdd("MyPlanet.UpdateAfterSimulation100");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer) { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateOnceBeforeFrame(ref ProfilerTimer __local_timer, MyPlanet __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateOnceBeforeFrame, profileMemory: true,
            new(__instance, "Planet: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer, MyPlanet __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation10, profileMemory: true,
            new(__instance, "Planet: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdatePlanetPhysics(ref ProfilerTimer __local_timer, MyPlanet __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdatePlanetPhysics, profileMemory: true,
            new(__instance, "Planet: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer, MyPlanet __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation100, profileMemory: true,
            new(__instance, "Planet: {0}"));

        return true;
    }
}
