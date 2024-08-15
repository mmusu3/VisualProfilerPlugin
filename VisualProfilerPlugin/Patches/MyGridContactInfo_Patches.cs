using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Cube;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGridContactInfo_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, "ReadVoxelSurfaceMaterial", _public: false, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyGridContactInfo).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyGridContactInfo_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyGridContactInfo_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey ReadVoxelSurfaceMaterial;

        internal static void Init()
        {
            ReadVoxelSurfaceMaterial = ProfilerKeyCache.GetOrAdd("MyGridContactInfo.ReadVoxelSurfaceMaterial");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_ReadVoxelSurfaceMaterial(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ReadVoxelSurfaceMaterial); return true; }
}
