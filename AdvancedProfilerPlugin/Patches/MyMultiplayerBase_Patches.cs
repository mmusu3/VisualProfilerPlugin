using System.Runtime.CompilerServices;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyMultiplayerBase_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyMultiplayerBase).GetPublicInstanceMethod(nameof(MyMultiplayerBase.Tick));
        var prefix = typeof(MyMultiplayerBase_Patches).GetNonPublicStaticMethod(nameof(Prefix_Tick));
        var suffix = typeof(MyMultiplayerBase_Patches).GetNonPublicStaticMethod(nameof(Suffix_Tick));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Tick(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyMultiplayerBase.Tick");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Tick(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
