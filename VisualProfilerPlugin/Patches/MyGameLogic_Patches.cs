using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyGameLogic_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyGameLogic).GetPublicStaticMethod(nameof(MyGameLogic.UpdateOnceBeforeFrame));
        var prefix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateOnceBeforeFrame));
        var suffix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyGameLogic).GetPublicStaticMethod(nameof(MyGameLogic.UpdateBeforeSimulation));
        prefix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateBeforeSimulation));
        suffix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyGameLogic).GetPublicStaticMethod(nameof(MyGameLogic.UpdateAfterSimulation));
        prefix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateAfterSimulation));
        suffix = typeof(MyGameLogic_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey UpdateOnceBeforeFrame;
        internal static ProfilerKey UpdateBeforeSimulation;
        internal static ProfilerKey UpdateAfterSimulation;

        internal static void Init()
        {
            UpdateOnceBeforeFrame = ProfilerKeyCache.GetOrAdd("MyGameLogic.UpdateOnceBeforeFrame");
            UpdateBeforeSimulation = ProfilerKeyCache.GetOrAdd("MyGameLogic.UpdateBeforeSimulation");
            UpdateAfterSimulation = ProfilerKeyCache.GetOrAdd("MyGameLogic.UpdateAfterSimulation");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateOnceBeforeFrame(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.UpdateOnceBeforeFrame);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation);
        return true;
    }
}
