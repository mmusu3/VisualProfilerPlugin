using System.Runtime.CompilerServices;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyDedicatedServerBase_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyDedicatedServerBase).GetNonPublicInstanceMethod("UpdateSteamServerData");
        var prefix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateSteamServerData));
        var suffix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyDedicatedServerBase).GetNonPublicInstanceMethod("ClientConnected");
        prefix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_ClientConnected));
        suffix = typeof(MyDedicatedServerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey UpdateSteamServerData;
        internal static ProfilerKey ClientConnected;

        internal static void Init()
        {
            UpdateSteamServerData = ProfilerKeyCache.GetOrAdd("MyDedicatedServerBase.UpdateSteamServerData");
            ClientConnected = ProfilerKeyCache.GetOrAdd("MyDedicatedServerBase.ClientConnected");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateSteamServerData(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.UpdateSteamServerData); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ClientConnected(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ClientConnected); return true; }
}
