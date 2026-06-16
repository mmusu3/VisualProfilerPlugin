using System.Runtime.CompilerServices;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyScriptManager_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, "LoadData", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "LoadScripts", _public: false, _static: false);

        static bool PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
        {
            return PatchHelper.PatchPrefixSuffixPair(typeof(MyScriptManager), typeof(MyScriptManager_Patches), patchContext, methodName, _public, _static);
        }
    }

    static class Keys
    {
        internal static ProfilerKey LoadData;
        internal static ProfilerKey LoadScripts;

        internal static void Init()
        {
            LoadData = ProfilerKeyCache.GetOrAdd("MyScriptManager.LoadData");
            LoadScripts = ProfilerKeyCache.GetOrAdd("MyScriptManager.LoadScripts");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_LoadData(ref ProfilerTimer __local_timer)
    {
        var mods = MySession.Static.Mods;

        __local_timer = Profiler.Start(Keys.LoadData, ProfilerTimerOptions.ProfileMemory,
            new(mods?.Count ?? 0, "Mod Count: {0}"));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_LoadScripts(ref ProfilerTimer __local_timer, MyModContext mod)
    {
        __local_timer = Profiler.Start(Keys.LoadScripts, ProfilerTimerOptions.ProfileMemory,
            new(mod.ModName, "Mod: {0}"));

        return true;
    }
}
