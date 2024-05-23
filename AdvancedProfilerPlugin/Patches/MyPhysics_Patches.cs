using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Havok;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRageMath.Spatial;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyPhysics_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Transpile(ctx, nameof(MyPhysics.LoadData), _public: true, _static: false);
        Transpile(ctx, "UnloadData", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "SimulateInternal", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "ExecuteParallelRayCasts", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "StepWorldsMeasured", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "StepSingleWorld", _public: false, _static: false);
        Transpile(ctx, "StepWorldsParallel", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "EnableOptimizations", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "DisableOptimizations", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateActiveRigidBodies", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateCharacters", _public: false, _static: false);
    }

    static MethodInfo GetSourceMethod(string methodName, bool _public, bool _static)
    {
        MethodInfo source;

        if (_public)
        {
            if (_static)
                source = typeof(MyPhysics).GetPublicStaticMethod(methodName);
            else
                source = typeof(MyPhysics).GetPublicInstanceMethod(methodName);
        }
        else
        {
            if (_static)
                source = typeof(MyPhysics).GetNonPublicStaticMethod(methodName);
            else
                source = typeof(MyPhysics).GetNonPublicInstanceMethod(methodName);
        }

        return source;
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
        var suffix = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("Suffix_" + methodName);

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SimulateInternal(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.SimulateInternal");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SimulateInternal(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ExecuteParallelRayCasts(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.ExecuteParallelRayCasts");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ExecuteParallelRayCasts(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_StepWorldsMeasured(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.StepWorldsMeasured");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_StepWorldsMeasured(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_StepSingleWorld(ref ProfilerTimer __local_timer, HkWorld world)
    {
        __local_timer = Profiler.Start(0, "MyPhysics.StepSingleWorld", profileMemory: true, new(world));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_StepSingleWorld(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    static IEnumerable<MsilInstruction> Transpile_StepWorldsParallel(IEnumerable<MsilInstruction> instructionStream, MethodBody __methodBody, Func<Type, MsilLocal> __localCreator)
    {
        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.StepWorldsParallel.");

        const int expectedParts = 7;
        int patchedParts = 0;

        var profilerStartMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(bool), typeof(ProfilerEvent.ExtraData)]);
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

        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("StepWorldsParallel");
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_EnableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.EnableOptimizations");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_EnableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DisableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.DisableOptimizations");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_DisableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateActiveRigidBodies(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.UpdateActiveRigidBodies");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateActiveRigidBodies(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateCharacters(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.UpdateCharacters");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateCharacters(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
