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

        // TODO: Use transpilers to profile individual components
        PatchPrefixSuffixPair(ctx, "UpdateComponentsBeforeSimulation", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateComponentsBeforeSimulation100", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "SimulateComponents", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateComponentsAfterSimulation", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateComponentsAfterSimulation10", _public: false, _static: false);

        PatchPrefixSuffixPair(ctx, "RigidBody_ContactPointCallback", _public: false, _static: false);

        static bool PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
        {
            return PatchHelper.PatchPrefixSuffixPair(typeof(MyCharacter), typeof(MyCharacter_Patches), patchContext, methodName, _public, _static);
        }
    }

    static class Keys
    {
        internal static ProfilerKey UpdateComponentsBeforeSimulation;
        internal static ProfilerKey UpdateComponentsBeforeSimulation100;
        internal static ProfilerKey SimulateComponents;
        internal static ProfilerKey UpdateComponentsAfterSimulation;
        internal static ProfilerKey UpdateComponentsAfterSimulation10;
        internal static ProfilerKey RigidBody_ContactPointCallback;

        internal static void Init()
        {
            UpdateComponentsBeforeSimulation = ProfilerKeyCache.GetOrAdd("MyCharacter.UpdateComponentsBeforeSimulation");
            UpdateComponentsBeforeSimulation100 = ProfilerKeyCache.GetOrAdd("MyCharacter.UpdateComponentsBeforeSimulation100");
            SimulateComponents = ProfilerKeyCache.GetOrAdd("MyCharacter.SimulateComponents");
            UpdateComponentsAfterSimulation = ProfilerKeyCache.GetOrAdd("MyCharacter.UpdateComponentsAfterSimulation");
            UpdateComponentsAfterSimulation10 = ProfilerKeyCache.GetOrAdd("MyCharacter.UpdateComponentsAfterSimulation10");
            RigidBody_ContactPointCallback = ProfilerKeyCache.GetOrAdd("MyCharacter.RigidBody_ContactPointCallback");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateComponentsBeforeSimulation(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateComponentsBeforeSimulation, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateComponentsBeforeSimulation100(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateComponentsBeforeSimulation100, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SimulateComponents(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.SimulateComponents, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateComponentsAfterSimulation(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateComponentsAfterSimulation, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateComponentsAfterSimulation10(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.UpdateComponentsAfterSimulation10, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RigidBody_ContactPointCallback(ref ProfilerTimer __local_timer, MyCharacter __instance)
    {
        __local_timer = Profiler.Start(Keys.RigidBody_ContactPointCallback, ProfilerTimerOptions.ProfileMemory, new(__instance));

        return true;
    }
}
