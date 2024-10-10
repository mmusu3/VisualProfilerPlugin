using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sandbox.Definitions;
using Torch.Managers.PatchManager;
using VRage.Game;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyDefinitionManager_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyDefinitionManager.LoadData), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyDefinitionManager).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyDefinitionManager_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyDefinitionManager_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey LoadData;

        internal static void Init()
        {
            LoadData = ProfilerKeyCache.GetOrAdd("MyDefinitionManager.LoadData");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer) { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_LoadData(ref ProfilerTimer __local_timer, List<MyObjectBuilder_Checkpoint.ModItem> mods)
    {
        __local_timer = Profiler.Start(Keys.LoadData, profileMemory: true,
            new(mods.Count, "Mod Count: {0}"));

        return true;
    }
}
