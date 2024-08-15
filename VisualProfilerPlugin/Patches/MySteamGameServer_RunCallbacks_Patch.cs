using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MySteamGameServer_RunCallbacks_Patch
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = Type.GetType("VRage.Steam.MySteamGameServer, VRage.Steam")!.GetPublicInstanceMethod("RunCallbacks");
        var prefix = typeof(MySteamGameServer_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(Prefix_RunCallbacks));
        var suffix = typeof(MySteamGameServer_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey RunCallbacks;

        internal static void Init()
        {
            RunCallbacks = ProfilerKeyCache.GetOrAdd("MySteamGameServer.RunCallbacks");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RunCallbacks(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.RunCallbacks); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
