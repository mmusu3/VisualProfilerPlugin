using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Havok;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Utils;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage;
using VRage.Library.Utils;
using VRageMath.Spatial;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyPhysics_Patches
{
    [ReflectedMethod(Type = typeof(MyPhysics))]
    static Func<MyPhysics, int, int, bool> IsClusterActive = null!;

    [ReflectedMethod(Type = typeof(MyPhysics))]
    static Action<MyPhysics, HkWorld> StepSingleWorld = null!;

    [ReflectedMethod(Type = typeof(MyPhysics))]
    static Action<MyPhysics> StepWorldsParallel = null!;

    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        Transpile(ctx, nameof(MyPhysics.LoadData), _public: true, _static: false);
        Transpile(ctx, "UnloadData", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "SimulateInternal", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "ExecuteParallelRayCasts", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, "StepSingleWorld", _public: false, _static: false);
        Transpile(ctx, "StepWorldsParallel", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "EnableOptimizations", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "DisableOptimizations", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateActiveRigidBodies", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateCharacters", _public: false, _static: false);

        var stepWorldsInternal = GetSourceMethod("StepWorldsInternal", _public: false, _static: false);
        var replacement = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("StepWorldsInternal");

        ctx.GetPattern(stepWorldsInternal).Prefixes.Add(replacement);
    }

    static MethodInfo GetSourceMethod(string methodName, bool _public, bool _static)
    {
        return typeof(MyPhysics).GetMethod(methodName, _public, _static);
    }

    static void Transpile(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = GetSourceMethod(methodName, _public, _static);
        var transpiler = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("Transpile_" + methodName);

        patchContext.GetPattern(source).Transpilers.Add(transpiler);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = GetSourceMethod(methodName, _public, _static);
        var prefix = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey SimulateInternal;
        internal static ProfilerKey ExecuteParallelRayCasts;
        internal static ProfilerKey StepWorldsParallel;
        internal static ProfilerKey EnableOptimizations;
        internal static ProfilerKey DisableOptimizations;
        internal static ProfilerKey UpdateActiveRigidBodies;
        internal static ProfilerKey UpdateCharacters;
        internal static ProfilerKey StepWorldsMeasured;
        internal static ProfilerKey StepWorldsSequential;

        internal static void Init()
        {
            SimulateInternal = ProfilerKeyCache.GetOrAdd("MyPhysics.SimulateInternal");
            ExecuteParallelRayCasts = ProfilerKeyCache.GetOrAdd("MyPhysics.ExecuteParallelRayCasts");
            StepWorldsParallel = ProfilerKeyCache.GetOrAdd("StepWorldsParallel");
            EnableOptimizations = ProfilerKeyCache.GetOrAdd("MyPhysics.EnableOptimizations");
            DisableOptimizations = ProfilerKeyCache.GetOrAdd("MyPhysics.DisableOptimizations");
            UpdateActiveRigidBodies = ProfilerKeyCache.GetOrAdd("MyPhysics.UpdateActiveRigidBodies");
            UpdateCharacters = ProfilerKeyCache.GetOrAdd("MyPhysics.UpdateCharacters");
            StepWorldsMeasured = ProfilerKeyCache.GetOrAdd("MyPhysics.StepWorldsMeasured");
            StepWorldsSequential = ProfilerKeyCache.GetOrAdd("MyPhysics.StepWorldsSequential");
        }
    }

    static IEnumerable<MsilInstruction> Transpile_LoadData(IEnumerable<MsilInstruction> instructions)
    {
        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.{nameof(MyPhysics.LoadData)}.");

        var m_jobQueueField = typeof(MyPhysics).GetField("m_jobQueue", BindingFlags.NonPublic | BindingFlags.Static)!;
        var m_threadPoolField = typeof(MyPhysics).GetField("m_threadPool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var initProfilingMethod = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(InitProfiling));

        bool patched = false;

        foreach (var ins in instructions)
        {
            yield return ins;

            if (ins.OpCode == OpCodes.Stsfld && ins.Operand is MsilOperandInline<FieldInfo> fieldOp && fieldOp.Value == m_jobQueueField)
            {
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(m_jobQueueField);
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(m_threadPoolField);
                yield return new MsilInstruction(OpCodes.Call).InlineValue(initProfilingMethod);
                patched = true;
            }
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.{nameof(MyPhysics.LoadData)}");
    }

    static void InitProfiling(HkJobQueue m_jobQueue, HkJobThreadPool m_threadPool)
    {
        // TODO: May add too much overhead
        HkTaskProfiler.HookJobQueue(m_jobQueue);

        Profiler.SetSortingGroupOrderPriority("Havok", 50);

        m_threadPool.RunOnEachWorker(delegate
        {
            int thisThreadIndex = m_threadPool.GetThisThreadIndex();
            Profiler.SetSortingGroupForCurrentThread("Havok", thisThreadIndex);
            Profiler.SetIsRealtimeThread(true);
        });
    }

    static IEnumerable<MsilInstruction> Transpile_UnloadData(IEnumerable<MsilInstruction> instructionStream)
    {
        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.UnloadData.");

        bool patched = false;

        var m_threadPoolField = typeof(MyPhysics).GetField("m_threadPool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var disposeMethod = typeof(HkHandle).GetPublicInstanceMethod(nameof(HkHandle.Dispose));
        var removeThreadsMethod = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(RemoveThreads));

        var instructions = instructionStream.ToArray();

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldsfld && ins.Operand is MsilOperandInline<FieldInfo> fieldOp && fieldOp.Value == m_threadPoolField)
            {
                if (instructions[i + 1].OpCode == OpCodes.Callvirt && instructions[i + 1].Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == disposeMethod)
                {
                    yield return new MsilInstruction(OpCodes.Ldarg_0);
                    yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(m_threadPoolField);
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(removeThreadsMethod);
                    patched = true;
                }
            }

            yield return ins;
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.UnloadData.");
    }

    static void RemoveThreads(HkJobThreadPool m_threadPool)
    {
        m_threadPool.RunOnEachWorker(Profiler.RemoveGroupForCurrentThread);
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(Inline)] static bool Prefix_SimulateInternal(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SimulateInternal); return true; }

    [MethodImpl(Inline)] static bool Prefix_ExecuteParallelRayCasts(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ExecuteParallelRayCasts); return true; }

    [MethodImpl(Inline)] static bool Prefix_StepSingleWorld(ref ProfilerTimer __local_timer, HkWorld world)
    { __local_timer = Profiler.Start(0, "MyPhysics.StepSingleWorld", profileMemory: true, new(world)); return true; }

    static IEnumerable<MsilInstruction> Transpile_StepWorldsParallel(IEnumerable<MsilInstruction> instructionStream, MethodBody __methodBody, Func<Type, MsilLocal> __localCreator)
    {
        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.StepWorldsParallel.");

        const int expectedParts = 7;
        int patchedParts = 0;

        var profilerKeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null);
        var profilerStartMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(bool), typeof(ProfilerEvent.ExtraData)]);
        var profilerStartMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(int), typeof(string)]);
        var profilerStartMethod3 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(int), typeof(string), typeof(bool), typeof(ProfilerEvent.ExtraData)]);
        var profilerStopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));
        var profilerEventExtraDataCtor1 = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null);
        var profilerEventExtraDataCtor2 = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null);

        var clustersField = typeof(MyPhysics).GetField(nameof(MyPhysics.Clusters));
        var clustersField2 = typeof(MyClusterTree).GetField("m_clusters", BindingFlags.Instance | BindingFlags.NonPublic);
        var listCountGetter = typeof(List<MyClusterTree.MyCluster>).GetProperty(nameof(List<MyClusterTree.MyCluster>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod;
        var executePendingCriticalOperationsMethod = typeof(HkWorld).GetPublicInstanceMethod(nameof(HkWorld.ExecutePendingCriticalOperations));
        var initMTStepMethod = typeof(HkWorld).GetPublicInstanceMethod(nameof(HkWorld.InitMtStep));
        var waitPolicySetter = typeof(HkJobQueue).GetProperty(nameof(HkJobQueue.WaitPolicy), BindingFlags.Instance | BindingFlags.Public)?.SetMethod;
        var processAllJobsMethod = typeof(HkJobQueue).GetPublicInstanceMethod(nameof(HkJobQueue.ProcessAllJobs));
        var waitForCompletionMethod = typeof(HkJobThreadPool).GetPublicInstanceMethod(nameof(HkJobThreadPool.WaitForCompletion));
        var finishMtStepMethod = typeof(HkWorld).GetPublicInstanceMethod(nameof(HkWorld.FinishMtStep));
        var markForWriteMethod = typeof(HkWorld).GetPublicInstanceMethod(nameof(HkWorld.MarkForWrite));

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(Keys.StepWorldsParallel.GlobalIndex);
        yield return new MsilInstruction(OpCodes.Newobj).InlineValue(profilerKeyCtor);
        yield return new MsilInstruction(OpCodes.Ldc_I4_1); // profileMemory: true
        yield return new MsilInstruction(OpCodes.Ldsfld).InlineValue(clustersField);
        yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(clustersField2);
        yield return new MsilInstruction(OpCodes.Call).InlineValue(listCountGetter);
        yield return new MsilInstruction(OpCodes.Conv_I8);
        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Clusters: {0}");
        yield return new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor1);
        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod);
        yield return timerLocal1.AsValueStore();

        var instructions = instructionStream.ToArray();

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];
            var nextIns = i < instructions.Length - 1 ? instructions[i + 1] : null;

            if (ins.OpCode == OpCodes.Ldloc_S && nextIns != null)
            {
                if (nextIns.OpCode == OpCodes.Callvirt)
                {
                    if (nextIns.Operand is MsilOperandInline<MethodBase> call && call.Value == executePendingCriticalOperationsMethod)
                    {
                        var clusterLocal = __methodBody.LocalVariables.ElementAtOrDefault(4);

                        if (clusterLocal == null || clusterLocal.LocalType != typeof(MyClusterTree.MyCluster))
                        {
                            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.StepWorldsParallel. Failed to find cluster local variable.");
                        }
                        else
                        {
                            yield return new MsilInstruction(OpCodes.Ldc_I4_0); // Block 0
                            yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Init HKWorld update");
                            yield return new MsilInstruction(OpCodes.Ldc_I4_0); // profileMemory: false
                            yield return new MsilInstruction(OpCodes.Ldloc_S).InlineValue(new MsilLocal(clusterLocal.LocalIndex));
                            yield return new MsilInstruction(OpCodes.Ldnull);
                            yield return new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor2);
                            yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod3);
                            yield return timerLocal2.AsValueStore();
                            patchedParts++;
                        }
                    }
                }
                else if (nextIns.OpCode == OpCodes.Ldsfld && i < instructions.Length - 3 && instructions[i + 3].OpCode == OpCodes.Callvirt)
                {
                    if (instructions[i + 3].Operand is MsilOperandInline<MethodBase> call && call.Value == finishMtStepMethod)
                    {
                        yield return new MsilInstruction(OpCodes.Ldc_I4_3); // Block 3
                        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Finish HKWorld update");
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod2);
                        yield return timerLocal2.AsValueStore();
                        patchedParts++;
                    }
                }
            }
            else if (ins.OpCode == OpCodes.Ldsfld && nextIns != null && nextIns.OpCode == OpCodes.Ldc_I4_0
                && i < instructions.Length - 2 && instructions[i + 2].OpCode == OpCodes.Callvirt)
            {
                if (instructions[i + 2].Operand is MsilOperandInline<MethodBase> call && call.Value == waitPolicySetter)
                {
                    yield return new MsilInstruction(OpCodes.Ldc_I4_1).SwapTryCatchOperations(ins); // Block 1
                    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Process jobs");
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod2);
                    yield return timerLocal2.AsValueStore();
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            yield return ins;

            if (ins.OpCode == OpCodes.Pop && i > 0 && instructions[i - 1].OpCode == OpCodes.Callvirt)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == initMTStepMethod)
                {
                    yield return timerLocal2.AsValueLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Callvirt)
            {
                if (ins.Operand is MsilOperandInline<MethodBase> call)
                {
                    if (call.Value == processAllJobsMethod)
                    {
                        yield return timerLocal2.AsValueLoad();
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
                        // Start next
                        yield return new MsilInstruction(OpCodes.Ldc_I4_2); // Block 2
                        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Wait for Havok thread pool");
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod2);
                        yield return timerLocal2.AsValueStore();
                        patchedParts++;
                    }
                    else if (call.Value == waitForCompletionMethod)
                    {
                        yield return timerLocal2.AsValueLoad();
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
                        patchedParts++;
                    }
                    else if (call.Value == markForWriteMethod)
                    {
                        yield return timerLocal2.AsValueLoad();
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
                        patchedParts++;
                    }
                }
            }
        }

        yield return timerLocal1.AsValueLoad().SwapLabelsAndTryCatchOperations(instructions[^1]);
        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
        yield return new MsilInstruction(OpCodes.Ret);

        if (patchedParts != expectedParts)
            Plugin.Log.Fatal($"Failed to patch {nameof(MyPhysics)}.StepWorldsParallel. {patchedParts} out of {expectedParts} code parts matched.");
        else
            Plugin.Log.Debug("Patch successful.");
    }

    [MethodImpl(Inline)] static bool Prefix_EnableOptimizations(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.EnableOptimizations); return true; }

    [MethodImpl(Inline)] static bool Prefix_DisableOptimizations(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DisableOptimizations); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateActiveRigidBodies(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.UpdateActiveRigidBodies); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateCharacters(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.UpdateCharacters); return true; }

    static bool StepWorldsInternal(MyPhysics __instance, List<MyTuple<HkWorld, MyTimeSpan>>? timings)
    {
        if (timings != null)
        {
            StepWorldsMeasured(__instance, timings);
        }
        else if (MyFakes.ENABLE_HAVOK_PARALLEL_SCHEDULING && !Profiler.IsRecordingEvents) // TODO: May want to add a dedicated option
        {
            StepWorldsParallel(__instance);
        }
        else
        {
            StepWorldsSequential(__instance);
        }

        if (HkBaseSystem.IsOutOfMemory)
            throw new OutOfMemoryException("Havok run out of memory");

        return false;
    }

    static void StepWorldsMeasured(MyPhysics __instance, List<MyTuple<HkWorld, MyTimeSpan>> timings)
    {
        var clusters = MyPhysics.Clusters.GetClusters();

        using var _ = Profiler.Start(Keys.StepWorldsMeasured, profileMemory: true, new(clusters.Count, "Num Clusters: {0}"));

        foreach (var cluster in clusters)
        {
            var world = (HkWorld)cluster.UserData;
            long start = Stopwatch.GetTimestamp();

            using (Profiler.Start(0, "MyPhysics.StepSingleWorld", profileMemory: true, new(cluster)))
                StepSingleWorld(__instance, world);

            long end = Stopwatch.GetTimestamp();
            timings.Add(MyTuple.Create(world, MyTimeSpan.FromTicks(end - start)));
        }
    }

    static void StepWorldsSequential(MyPhysics __instance)
    {
        var clusters = MyPhysics.Clusters.GetClusters();

        using var _ = Profiler.Start(Keys.StepWorldsSequential, profileMemory: true, new(clusters.Count, "Num Clusters: {0}"));

        foreach (var cluster in clusters)
        {
            if (cluster.UserData is HkWorld world && IsClusterActive(__instance, cluster.ClusterId, world.CharacterRigidBodies.Count))
            {
                using (Profiler.Start(0, "MyPhysics.StepSingleWorld", profileMemory: true, new(cluster)))
                    StepSingleWorld(__instance, world);
            }
        }
    }
}
