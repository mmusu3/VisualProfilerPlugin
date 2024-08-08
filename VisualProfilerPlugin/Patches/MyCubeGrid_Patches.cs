using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyCubeGrid_Patches
{
    public static void Patch(PatchContext ctx)
    {
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation100), _public: true, _static: false);

        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation100), _public: true, _static: false);

        // TODO: Wrap Invoke call in Dispatch with transpiler
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyCubeGrid).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start("MyCubeGrid.UpdateBeforeSimulation", profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start("MyCubeGrid.UpdateBeforeSimulation10", profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start("MyCubeGrid.UpdateBeforeSimulation100", profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start("MyCubeGrid.UpdateAfterSimulation", profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start("MyCubeGrid.UpdateAfterSimulation10", profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start("MyCubeGrid.UpdateAfterSimulation100", profileMemory: true, new(__instance)); return true; }
}
