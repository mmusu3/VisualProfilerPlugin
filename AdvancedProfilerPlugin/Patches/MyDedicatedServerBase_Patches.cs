using System.Runtime.CompilerServices;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyDedicatedServerBase_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyDedicatedServerBase).GetNonPublicInstanceMethod("UpdateSteamServerData");
        var prefix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateSteamServerData));
        var suffix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix_UpdateSteamServerData));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyDedicatedServerBase).GetNonPublicInstanceMethod("ClientConnected");
        prefix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_ClientConnected));
        suffix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix_ClientConnected));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateSteamServerData(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyDedicatedServerBase.UpdateSteamServerData");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateSteamServerData(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ClientConnected(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyDedicatedServerBase.ClientConnected");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ClientConnected(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
