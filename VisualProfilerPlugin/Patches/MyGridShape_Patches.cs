using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGridShape_Patches
{
    public static void Patch(PatchContext ctx)
    {
        PatchPrefixSuffixPair(ctx, "UpdateDirtyBlocks", _public: false, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyGridShape).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyGridShape_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyGridShape_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_UpdateDirtyBlocks(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start("MyGridShape.UpdateDirtyBlocks"); return true; }
}
