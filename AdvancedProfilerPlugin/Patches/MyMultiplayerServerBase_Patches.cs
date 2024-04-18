using System.Runtime.CompilerServices;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyMultiplayerServerBase_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyMultiplayerServerBase).GetNonPublicInstanceMethod("ClientReady");
        var prefix = typeof(MyMultiplayerServerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_ClientReady));
        var suffix = typeof(MyMultiplayerServerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix_ClientReady));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ClientReady(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyMultiplayerServerBase.ClientReady");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ClientReady(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
