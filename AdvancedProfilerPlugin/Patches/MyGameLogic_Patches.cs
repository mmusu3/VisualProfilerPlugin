using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyGameLogic_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyGameLogic).GetPublicStaticMethod(nameof(MyGameLogic.UpdateOnceBeforeFrame));
        var prefix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateOnceBeforeFrame));
        var suffix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Suffix_UpdateOnceBeforeFrame));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyGameLogic).GetPublicStaticMethod(nameof(MyGameLogic.UpdateBeforeSimulation));
        prefix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateBeforeSimulation));
        suffix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Suffix_UpdateBeforeSimulation));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyGameLogic).GetPublicStaticMethod(nameof(MyGameLogic.UpdateAfterSimulation));
        prefix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateAfterSimulation));
        suffix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Suffix_UpdateAfterSimulation));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateOnceBeforeFrame(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyGameLogic.UpdateOnceBeforeFrame");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateOnceBeforeFrame(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyGameLogic.UpdateBeforeSimulation");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyGameLogic.UpdateAfterSimulation");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateAfterSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
