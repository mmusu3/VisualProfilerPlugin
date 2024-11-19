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
    internal static bool ProfileEachCluster;

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
        internal static ProfilerKey StepWorldsMeasured;
        internal static ProfilerKey StepWorldsSequential;
        internal static ProfilerKey EnableOptimizations;
        internal static ProfilerKey DisableOptimizations;
        internal static ProfilerKey UpdateActiveRigidBodies;
        internal static ProfilerKey UpdateCharacters;

        internal static void Init()
        {
            SimulateInternal = ProfilerKeyCache.GetOrAdd("MyPhysics.SimulateInternal");
            ExecuteParallelRayCasts = ProfilerKeyCache.GetOrAdd("MyPhysics.ExecuteParallelRayCasts");
            StepWorldsParallel = ProfilerKeyCache.GetOrAdd("StepWorldsParallel");
            StepWorldsMeasured = ProfilerKeyCache.GetOrAdd("MyPhysics.StepWorldsMeasured");
            StepWorldsSequential = ProfilerKeyCache.GetOrAdd("MyPhysics.StepWorldsSequential");
            EnableOptimizations = ProfilerKeyCache.GetOrAdd("MyPhysics.EnableOptimizations");
            DisableOptimizations = ProfilerKeyCache.GetOrAdd("MyPhysics.DisableOptimizations");
            UpdateActiveRigidBodies = ProfilerKeyCache.GetOrAdd("MyPhysics.UpdateActiveRigidBodies");
            UpdateCharacters = ProfilerKeyCache.GetOrAdd("MyPhysics.UpdateCharacters");
        }
    }

    static IEnumerable<MsilInstruction> Transpile_LoadData(IEnumerable<MsilInstruction> instructionStream)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.{nameof(MyPhysics.LoadData)}.");

        var m_jobQueueField = typeof(MyPhysics).GetField("m_jobQueue", BindingFlags.NonPublic | BindingFlags.Static)!;
        var m_threadPoolField = typeof(MyPhysics).GetField("m_threadPool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var initProfilingMethod = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(InitProfiling));

        bool patched = false;

        foreach (var ins in instructions)
        {
            Emit(ins);

            if (ins.OpCode == OpCodes.Stsfld && ins.Operand is MsilOperandInline<FieldInfo> fieldOp && fieldOp.Value == m_jobQueueField)
            {
                Emit(new MsilInstruction(OpCodes.Ldarg_0));
                Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(m_jobQueueField));
                Emit(new MsilInstruction(OpCodes.Ldarg_0));
                Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(m_threadPoolField));
                Emit(new MsilInstruction(OpCodes.Call).InlineValue(initProfilingMethod));
                patched = true;
            }
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.{nameof(MyPhysics.LoadData)}");

        return patched ? newInstructions : instructions;
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
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.UnloadData.");

        bool patched = false;

        var m_threadPoolField = typeof(MyPhysics).GetField("m_threadPool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var disposeMethod = typeof(HkHandle).GetPublicInstanceMethod(nameof(HkHandle.Dispose));
        var removeThreadsMethod = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(RemoveThreads));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldsfld && ins.Operand is MsilOperandInline<FieldInfo> fieldOp && fieldOp.Value == m_threadPoolField)
            {
                if (instructions[i + 1].OpCode == OpCodes.Callvirt && instructions[i + 1].Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == disposeMethod)
                {
                    Emit(new MsilInstruction(OpCodes.Ldarg_0));
                    Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(m_threadPoolField));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(removeThreadsMethod));
                    patched = true;
                }
            }

            Emit(ins);
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.UnloadData.");

        return patched ? newInstructions : instructions;
    }

    static void RemoveThreads(HkJobThreadPool m_threadPool)
    {
        m_threadPool.RunOnEachWorker(Profiler.RemoveGroupForCurrentThread);
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(Inline)]
    static bool Prefix_SimulateInternal(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.SimulateInternal, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.Physics));
        return true;
    }

    [MethodImpl(Inline)] static bool Prefix_ExecuteParallelRayCasts(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ExecuteParallelRayCasts); return true; }

    [MethodImpl(Inline)] static bool Prefix_StepSingleWorld(ref ProfilerTimer __local_timer, HkWorld world)
    { __local_timer = Profiler.Start(0, "MyPhysics.StepSingleWorld", ProfilerTimerOptions.ProfileMemory, new(world)); return true; }

    static IEnumerable<MsilInstruction> Transpile_StepWorldsParallel(IEnumerable<MsilInstruction> instructionStream, MethodBody __methodBody, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.StepWorldsParallel.");

        const int expectedParts = 7;
        int patchedParts = 0;

        var profilerKeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null);
        var profilerStartMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)]);
        var profilerStartMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(int), typeof(string)]);
        var profilerStartMethod3 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(int), typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)]);
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

        Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue(Keys.StepWorldsParallel.GlobalIndex));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerKeyCtor));
        Emit(new MsilInstruction(OpCodes.Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
        Emit(new MsilInstruction(OpCodes.Ldsfld).InlineValue(clustersField));
        Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(clustersField2));
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(listCountGetter));
        Emit(new MsilInstruction(OpCodes.Conv_I8));
        Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Clusters: {0}"));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor1));
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod));
        Emit(timerLocal1.AsValueStore());

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
                            Emit(new MsilInstruction(OpCodes.Ldc_I4_0)); // Block 0
                            Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Init HKWorld update"));
                            Emit(new MsilInstruction(OpCodes.Ldc_I4_0)); // profileMemory: false
                            Emit(new MsilInstruction(OpCodes.Ldloc_S).InlineValue(new MsilLocal(clusterLocal.LocalIndex)));
                            Emit(new MsilInstruction(OpCodes.Ldnull));
                            Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor2));
                            Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod3));
                            Emit(timerLocal2.AsValueStore());
                            patchedParts++;
                        }
                    }
                }
                else if (nextIns.OpCode == OpCodes.Ldsfld && i < instructions.Length - 3 && instructions[i + 3].OpCode == OpCodes.Callvirt)
                {
                    if (instructions[i + 3].Operand is MsilOperandInline<MethodBase> call && call.Value == finishMtStepMethod)
                    {
                        var clusterLocal = __methodBody.LocalVariables.ElementAtOrDefault(6);

                        if (clusterLocal == null || clusterLocal.LocalType != typeof(MyClusterTree.MyCluster))
                        {
                            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.StepWorldsParallel. Failed to find cluster local variable.");
                        }
                        else
                        {
                            Emit(new MsilInstruction(OpCodes.Ldc_I4_3)); // Block 3
                            Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Finish HKWorld update"));
                            Emit(new MsilInstruction(OpCodes.Ldc_I4_0)); // profileMemory: false
                            Emit(new MsilInstruction(OpCodes.Ldloc_S).InlineValue(new MsilLocal(clusterLocal.LocalIndex)));
                            Emit(new MsilInstruction(OpCodes.Ldnull));
                            Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor2));
                            Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod3));
                            Emit(timerLocal2.AsValueStore());
                            patchedParts++;
                        }
                    }
                }
            }
            else if (ins.OpCode == OpCodes.Ldsfld && nextIns != null && nextIns.OpCode == OpCodes.Ldc_I4_0
                && i < instructions.Length - 2 && instructions[i + 2].OpCode == OpCodes.Callvirt)
            {
                if (instructions[i + 2].Operand is MsilOperandInline<MethodBase> call && call.Value == waitPolicySetter)
                {
                    Emit(new MsilInstruction(OpCodes.Ldc_I4_1).SwapTryCatchOperations(ref ins)); // Block 1
                    Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Process jobs"));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod2));
                    Emit(timerLocal2.AsValueStore());
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            Emit(ins);

            if (ins.OpCode == OpCodes.Pop && i > 0 && instructions[i - 1].OpCode == OpCodes.Callvirt)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == initMTStepMethod)
                {
                    Emit(timerLocal2.AsValueLoad());
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod));
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Callvirt)
            {
                if (ins.Operand is MsilOperandInline<MethodBase> call)
                {
                    if (call.Value == processAllJobsMethod)
                    {
                        Emit(timerLocal2.AsValueLoad());
                        Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod));
                        // Start next
                        Emit(new MsilInstruction(OpCodes.Ldc_I4_2)); // Block 2
                        Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Wait for Havok thread pool"));
                        Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod2));
                        Emit(timerLocal2.AsValueStore());
                        patchedParts++;
                    }
                    else if (call.Value == waitForCompletionMethod)
                    {
                        Emit(timerLocal2.AsValueLoad());
                        Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod));
                        patchedParts++;
                    }
                    else if (call.Value == markForWriteMethod)
                    {
                        Emit(timerLocal2.AsValueLoad());
                        Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod));
                        patchedParts++;
                    }
                }
            }
        }

        Emit(timerLocal1.AsValueLoad().CopyLabelsAndTryCatchOperations(instructions[^1]));
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod));
        Emit(new MsilInstruction(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyPhysics)}.StepWorldsParallel. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
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
        else if (!MyFakes.ENABLE_HAVOK_PARALLEL_SCHEDULING || (Profiler.IsRecordingEvents && ProfileEachCluster))
        {
            StepWorldsSequential(__instance);
        }
        else
        {
            StepWorldsParallel(__instance);
        }

        if (HkBaseSystem.IsOutOfMemory)
            throw new OutOfMemoryException("Havok run out of memory");

        return false;
    }

    static void StepWorldsMeasured(MyPhysics __instance, List<MyTuple<HkWorld, MyTimeSpan>> timings)
    {
        var clusters = MyPhysics.Clusters.GetClusters();

        using var _ = Profiler.Start(Keys.StepWorldsMeasured, ProfilerTimerOptions.ProfileMemory, new(clusters.Count, "Num Clusters: {0}"));

        foreach (var cluster in clusters)
        {
            var world = (HkWorld)cluster.UserData;
            long start = Stopwatch.GetTimestamp();

            using (Profiler.Start(0, "MyPhysics.StepSingleWorld", ProfilerTimerOptions.ProfileMemory, new(cluster)))
                StepSingleWorld(__instance, world);

            long end = Stopwatch.GetTimestamp();
            timings.Add(MyTuple.Create(world, MyTimeSpan.FromTicks(end - start)));
        }
    }

    static void StepWorldsSequential(MyPhysics __instance)
    {
        var clusters = MyPhysics.Clusters.GetClusters();

        using var _ = Profiler.Start(Keys.StepWorldsSequential, ProfilerTimerOptions.ProfileMemory, new(clusters.Count, "Num Clusters: {0}"));

        foreach (var cluster in clusters)
        {
            if (cluster.UserData is HkWorld world && IsClusterActive(__instance, cluster.ClusterId, world.CharacterRigidBodies.Count))
            {
                using (Profiler.Start(0, "MyPhysics.StepSingleWorld", ProfilerTimerOptions.ProfileMemory, new(cluster)))
                    StepSingleWorld(__instance, world);
            }
        }
    }
}
