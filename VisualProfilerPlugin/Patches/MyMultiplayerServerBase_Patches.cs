using System.Runtime.CompilerServices;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyMultiplayerServerBase_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyMultiplayerServerBase).GetNonPublicInstanceMethod("ClientReady");
        var prefix = typeof(MyMultiplayerServerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_ClientReady));
        var suffix = typeof(MyMultiplayerServerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey ClientReady;

        internal static void Init()
        {
            ClientReady = ProfilerKeyCache.GetOrAdd("MyMultiplayerServerBase.ClientReady");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ClientReady(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ClientReady); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
