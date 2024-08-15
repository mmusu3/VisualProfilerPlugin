using System.Runtime.CompilerServices;
using Sandbox.Game.GameSystems;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGridConveyorSystem_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyGridConveyorSystem.UpdateBeforeSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyGridConveyorSystem.UpdateAfterSimulation100), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyGridConveyorSystem).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyGridConveyorSystem_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyGridConveyorSystem_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey UpdateBeforeSimulation10;
        internal static ProfilerKey UpdateAfterSimulation100;

        internal static void Init()
        {
            UpdateBeforeSimulation10 = ProfilerKeyCache.GetOrAdd("MyGridConveyorSystem.UpdateBeforeSimulation10");
            UpdateAfterSimulation100 = ProfilerKeyCache.GetOrAdd("MyGridConveyorSystem.UpdateAfterSimulation100");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation10); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation100); return true; }
}
