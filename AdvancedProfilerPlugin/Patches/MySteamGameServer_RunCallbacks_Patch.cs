using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MySteamGameServer_RunCallbacks_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = Type.GetType("VRage.Steam.MySteamGameServer, VRage.Steam")!.GetPublicInstanceMethod("RunCallbacks");
        var prefix = typeof(MySteamGameServer_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(Prefix_RunCallbacks));
        var suffix = typeof(MySteamGameServer_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(Suffix_RunCallbacks));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RunCallbacks(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MySteamGameServer.RunCallbacks");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_RunCallbacks(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
