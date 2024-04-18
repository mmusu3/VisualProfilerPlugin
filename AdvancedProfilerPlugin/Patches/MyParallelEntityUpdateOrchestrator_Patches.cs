using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.Collections;
using VRage.Game.Entity;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyParallelEntityUpdateOrchestrator_Patches
{
    public static void Patch(PatchContext ctx)
    {
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchOnceBeforeFrame), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchBeforeSimulation), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateBeforeSimulation", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateBeforeSimulation10", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateBeforeSimulation100", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchSimulate), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchAfterSimulation), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateAfterSimulation", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateAfterSimulation10", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateAfterSimulation100", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "PerformParallelUpdate", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        MethodInfo source;

        if (_public)
        {
            if (_static)
                source = typeof(MyParallelEntityUpdateOrchestrator).GetPublicStaticMethod(methodName);
            else
                source = typeof(MyParallelEntityUpdateOrchestrator).GetPublicInstanceMethod(methodName);
        }
        else
        {
            if (_static)
                source = typeof(MyParallelEntityUpdateOrchestrator).GetNonPublicStaticMethod(methodName);
            else
                source = typeof(MyParallelEntityUpdateOrchestrator).GetNonPublicInstanceMethod(methodName);
        }

        var prefix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Suffix_" + methodName);

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    #region Before

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DispatchOnceBeforeFrame(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyParallelEntityUpdateOrchestrator.DispatchOnceBeforeFrame");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_DispatchOnceBeforeFrame(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DispatchBeforeSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyParallelEntityUpdateOrchestrator.DispatchBeforeSimulation");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_DispatchBeforeSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer, HashSet<MyEntity> __field_m_entitiesForUpdate)
    {
        __local_timer = Profiler.Start("UpdateBeforeSimulation", __field_m_entitiesForUpdate.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10Heavy)
    {
        __local_timer = Profiler.Start("UpdateBeforeSimulation10", __field_m_entitiesForUpdate10.Count + __field_m_entitiesForUpdate10Heavy.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100Heavy)
    {
        __local_timer = Profiler.Start("UpdateBeforeSimulation100", __field_m_entitiesForUpdate100.Count + __field_m_entitiesForUpdate100Heavy.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DispatchSimulate(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyParallelEntityUpdateOrchestrator.DispatchSimulate");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_DispatchSimulate(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    #region After

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DispatchAfterSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyParallelEntityUpdateOrchestrator.DispatchAfterSimulation");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_DispatchAfterSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer,
        HashSet<MyEntity> __field_m_entitiesForUpdate, HashSet<MyEntity> __field_m_entitiesForUpdateAfter)
    {
        __local_timer = Profiler.Start("UpdateAfterSimulation", __field_m_entitiesForUpdate.Count + __field_m_entitiesForUpdateAfter.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateAfterSimulation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10Heavy)
    {
        __local_timer = Profiler.Start("UpdateAfterSimulation10", __field_m_entitiesForUpdate10.Count + __field_m_entitiesForUpdate10Heavy.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100Heavy)
    {
        __local_timer = Profiler.Start("UpdateAfterSimulation100", __field_m_entitiesForUpdate100.Count + __field_m_entitiesForUpdate100Heavy.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_PerformParallelUpdate(ref ProfilerTimer __local_timer,
        HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelFirst, HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelLast)
    {
        __local_timer = Profiler.Start("PerformParallelUpdate", __field_m_entitiesForUpdateParallelFirst.Count + __field_m_entitiesForUpdateParallelLast.Count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_PerformParallelUpdate(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ProcessInvokeLater(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("ProcessInvokeLater");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ProcessInvokeLater(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
