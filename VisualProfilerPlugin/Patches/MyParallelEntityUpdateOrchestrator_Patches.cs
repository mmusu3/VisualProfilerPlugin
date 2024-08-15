using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ParallelTasks;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.Collections;
using VRage.Game.Entity;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyParallelEntityUpdateOrchestrator_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

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
        //PatchPrefixSuffixPair(ctx, "PerformParallelUpdate", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater), _public: true, _static: false);

        var source = typeof(MyParallelEntityUpdateOrchestrator).GetNonPublicInstanceMethod("PerformParallelUpdate");
        var prefix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Prefix_PerformParallelUpdate");

        ctx.GetPattern(source).Prefixes.Add(prefix);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyParallelEntityUpdateOrchestrator).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey DispatchOnceBeforeFrame;
        internal static ProfilerKey DispatchBeforeSimulation;
        internal static ProfilerKey UpdateBeforeSimulation;
        internal static ProfilerKey UpdateBeforeSimulation10;
        internal static ProfilerKey UpdateBeforeSimulation100;
        internal static ProfilerKey DispatchSimulate;
        internal static ProfilerKey DispatchAfterSimulation;
        internal static ProfilerKey UpdateAfterSimulation;
        internal static ProfilerKey UpdateAfterSimulation10;
        internal static ProfilerKey UpdateAfterSimulation100;
        internal static ProfilerKey PerformParallelUpdate;
        internal static ProfilerKey ProcessInvokeLater;

        internal static void Init()
        {
            DispatchOnceBeforeFrame = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchOnceBeforeFrame");
            DispatchBeforeSimulation = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchBeforeSimulation");
            UpdateBeforeSimulation = ProfilerKeyCache.GetOrAdd("UpdateBeforeSimulation");
            UpdateBeforeSimulation10 = ProfilerKeyCache.GetOrAdd("UpdateBeforeSimulation10");
            UpdateBeforeSimulation100 = ProfilerKeyCache.GetOrAdd("UpdateBeforeSimulation100");
            DispatchSimulate = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchSimulate");
            DispatchAfterSimulation = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchAfterSimulation");
            UpdateAfterSimulation = ProfilerKeyCache.GetOrAdd("UpdateAfterSimulation");
            UpdateAfterSimulation10 = ProfilerKeyCache.GetOrAdd("UpdateAfterSimulation10");
            UpdateAfterSimulation100 = ProfilerKeyCache.GetOrAdd("UpdateAfterSimulation100");
            PerformParallelUpdate = ProfilerKeyCache.GetOrAdd("PerformParallelUpdate");
            ProcessInvokeLater = ProfilerKeyCache.GetOrAdd("ProcessInvokeLater");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    #region Before

    [MethodImpl(Inline)] static bool Prefix_DispatchOnceBeforeFrame(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchOnceBeforeFrame); return true; }

    [MethodImpl(Inline)] static bool Prefix_DispatchBeforeSimulation(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchBeforeSimulation); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer, HashSet<MyEntity> __field_m_entitiesForUpdate)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation, profileMemory: true, new(__field_m_entitiesForUpdate.Count, "Num entities: {0:n0}")); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation10, profileMemory: true,
            new(__field_m_entitiesForUpdate10.Count + __field_m_entitiesForUpdate10Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation100, profileMemory: true,
            new(__field_m_entitiesForUpdate100.Count + __field_m_entitiesForUpdate100Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    #endregion

    [MethodImpl(Inline)] static bool Prefix_DispatchSimulate(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchSimulate); return true; }

    #region After

    [MethodImpl(Inline)] static bool Prefix_DispatchAfterSimulation(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchAfterSimulation); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer,
        HashSet<MyEntity> __field_m_entitiesForUpdate, HashSet<MyEntity> __field_m_entitiesForUpdateAfter)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation, profileMemory: true,
            new(__field_m_entitiesForUpdate.Count + __field_m_entitiesForUpdateAfter.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation10, profileMemory: true,
            new(__field_m_entitiesForUpdate10.Count + __field_m_entitiesForUpdate10Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation100, profileMemory: true,
            new(__field_m_entitiesForUpdate100.Count + __field_m_entitiesForUpdate100Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    #endregion

    //[MethodImpl(Inline)]
    //static bool Prefix_PerformParallelUpdate(ref ProfilerTimer __local_timer,
    //    HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelFirst, HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelLast)
    //{
    //    __local_timer = Profiler.Start("PerformParallelUpdate", profileMemory: true,
    //        new(__field_m_entitiesForUpdateParallelFirst.Count + __field_m_entitiesForUpdateParallelLast.Count, "Num entities: {0:n0}"));

    //    return true;
    //}

    // There is some weird issue with using prefix + suffix that causes a long pause at
    // the end of the function so this is used instead as a workaround.
    //
    static bool Prefix_PerformParallelUpdate(Action<IMyParallelUpdateable> updateFunction, IEnumerable<IMyParallelUpdateable> __field_m_helper,
        HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelFirst, HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelLast)
    {
        using var stateToken = Havok.HkAccessControl.PushState(Havok.HkAccessControl.AccessState.SharedRead);

        using (Profiler.Start(Keys.PerformParallelUpdate, profileMemory: true,
            new(__field_m_entitiesForUpdateParallelFirst.Count + __field_m_entitiesForUpdateParallelLast.Count, "Num entities: {0:n0}")))
        {
            if (MyParallelEntityUpdateOrchestrator.ForceSerialUpdate)
            {
                foreach (var updatable in __field_m_helper)
                    updateFunction(updatable);
            }
            else
            {
                using (MyEntities.StartAsyncUpdateBlock())
                    Parallel.ForEach(__field_m_helper, updateFunction, MyParallelEntityUpdateOrchestrator.WorkerPriority, blocking: true);
            }
        }

        return false;
    }

    [MethodImpl(Inline)] static bool Prefix_ProcessInvokeLater(ref ProfilerTimer __local_timer)
    {
        // TODO: Wrap the invokes
        __local_timer = Profiler.Start(Keys.ProcessInvokeLater);
        return true;
    }
}
