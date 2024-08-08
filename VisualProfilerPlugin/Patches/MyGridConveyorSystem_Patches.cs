using System.Runtime.CompilerServices;
using Sandbox.Game.GameSystems;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGridConveyorSystem_Patches
{
    public static void Patch(PatchContext ctx)
    {
        PatchPrefixSuffixPair(ctx, nameof(MyGridConveyorSystem.UpdateBeforeSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyGridConveyorSystem.UpdateAfterSimulation100), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyGridConveyorSystem).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyGridConveyorSystem_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyGridConveyorSystem_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start("MyGridConveyorSystem.UpdateBeforeSimulation10"); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start("MyGridConveyorSystem.UpdateAfterSimulation100"); return true; }
}
