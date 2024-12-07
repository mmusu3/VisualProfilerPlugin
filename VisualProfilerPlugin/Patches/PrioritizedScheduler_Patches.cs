using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using ParallelTasks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using static VisualProfiler.TranspileHelper;

namespace VisualProfiler.Patches;

static class PrioritizedScheduler_Patches
{
    #region Worker Init

    // Hold a reference here
    static MethodRewritePattern initWorkersPattern = null!;

    public static void Patch()
    {
        Plugin.Log.Info("Begining early patch of PrioritizedScheduler.InitializeWorkerArrays");

        var source = typeof(PrioritizedScheduler).GetNonPublicInstanceMethod("InitializeWorkerArrays");
        var suffix = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(Suffix_InitializeWorkerArrays));

        initWorkersPattern = PatchHelper.CreateRewritePattern(source);
        initWorkersPattern.Suffixes.Add(suffix);

        PatchHelper.CommitMethodPatches(initWorkersPattern);

        Plugin.Log.Info("Early patch completed.");
    }

    static void Suffix_InitializeWorkerArrays(object[] __field_m_workerArrays)
    {
        var workerArrayType = typeof(PrioritizedScheduler).GetNestedType("WorkerArray", BindingFlags.NonPublic)!;
        var workersField = workerArrayType.GetField("m_workers", BindingFlags.Instance | BindingFlags.NonPublic)!;

        for (int i = 0; i < __field_m_workerArrays.Length; i++)
        {
            var array = __field_m_workerArrays[i];
            var workers = (object[])workersField.GetValue(array)!;

            InitWorkerArray(workers);
        }
    }

    static void InitWorkerArray(object[] workers)
    {
        var workerType = typeof(PrioritizedScheduler).GetNestedType("Worker", BindingFlags.NonPublic)!;
        var threadField = workerType.GetField("m_thread", BindingFlags.Instance | BindingFlags.NonPublic)!;

        for (int i = 0; i < workers.Length; i++)
        {
            var worker = workers[i];
            var thread = (Thread)threadField.GetValue(worker)!;

            InitWorker(thread, i);
        }
    }

    static void InitWorker(Thread thread, int workerIndex)
    {
        var threadPriority = thread.Priority;
        var groupName = "Parallel_" + threadPriority;

        Profiler.SetSortingGroupOrderPriority(groupName, 20 + (int)threadPriority);

        var group = Profiler.CreateGroupForThread(thread);
        group.SortingGroup = groupName;
        group.OrderInSortingGroup = workerIndex;
        group.IsWaitingForFirstUse = true;

        if (threadPriority == ThreadPriority.Highest)
            group.IsRealtimeThread = true;
    }
#endregion

    static MethodInfo scheduleMethod = null!;

    // Not currently enabled. Patching ScheduleOnEachWorker causes
    // the instance argument to become corrupt somehow.
    public static void Patch(PatchContext ctx)
    {
        var wokerArrayType = typeof(PrioritizedScheduler).GetNestedType("WorkerArray", BindingFlags.NonPublic)!;
        var source = wokerArrayType.GetPublicInstanceMethod("ScheduleOnEachWorker");
        var pattern = ctx.GetPattern(source);

        //var transpiler = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(Transpile_WorkerArray_ScheduleOnEachWorker));
        //pattern.Transpilers.Add(transpiler);

        scheduleMethod = wokerArrayType.GetPublicInstanceMethod("Schedule");

        var prefix = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(Prefix_ScheduleOnEachWorker));
        pattern.Prefixes.Add(prefix);
    }

    static IEnumerable<MsilInstruction> Transpile_WorkerArray_ScheduleOnEachWorker(IEnumerable<MsilInstruction> instructionStream)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(PrioritizedScheduler)}.WorkerArray.ScheduleOnEachWorker.");

        const int expectedParts = 1;
        int patchedParts = 0;

        var createOptionsMethod = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(CreateWorkOptions));
        var defaultOptionsField = typeof(Parallel).GetField(nameof(Parallel.DefaultOptions), BindingFlags.Static | BindingFlags.Public);
        var workersGetter = typeof(PrioritizedScheduler).GetNestedType("WorkerArray", BindingFlags.NonPublic)!
            .GetProperty("Workers", BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        bool skipping = false;

        const int toSkip = 8;
        int skipped = 0;

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldsfld && ins.Operand is MsilOperandInline<FieldInfo> ldField && ldField.Value == defaultOptionsField)
            {
                e.Emit(new(OpCodes.Ldarg_1));
                e.Emit(new(OpCodes.Ldarg_0));
                e.Emit(Call(workersGetter));
                e.Emit(new(OpCodes.Ldlen));
                e.Emit(new(OpCodes.Conv_I4));
                e.Emit(Call(createOptionsMethod));
                skipping = true;
                patchedParts++;
            }
            else if (ins.OpCode == OpCodes.Newobj)
            {
                skipping = false;
            }

            if (skipping)
                skipped++;
            else
                e.Emit(ins);
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(PrioritizedScheduler)}.WorkerArray.ScheduleOnEachWorker. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else if (skipped != toSkip)
        {
            Plugin.Log.Error($"Failed to patch {nameof(PrioritizedScheduler)}.WorkerArray.ScheduleOnEachWorker. {skipped} out of {toSkip} instructions skipped.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static WorkOptions CreateWorkOptions(Action action, int numWorkers)
    {
        return new WorkOptions {
            MaximumThreads = numWorkers,
            TaskType = VRage.Profiler.MyProfiler.TaskType.WorkItem,
            DebugName = action.Method.Name
        };
    }

    static bool Prefix_ScheduleOnEachWorker(object __instance, Action action, Array __field_m_workers, ref Task __result)
    {
        var barrier = new Barrier(__field_m_workers.Length);

        var options = new WorkOptions {
            MaximumThreads = __field_m_workers.Length,
            TaskType = VRage.Profiler.MyProfiler.TaskType.WorkItem,
            DebugName = action.Method.Name
        };

        var work = new ActionWork(delegate
        {
            barrier.SignalAndWait();
            action();
        }, options);

        var workItem = WorkItem.Get();
        workItem.Callback = null;
        workItem.WorkData = null;
        workItem.CompletionCallbacks = null;

        Task task = workItem.PrepareStart(work);

        scheduleMethod.Invoke(__instance, [task]);
        __result = task;

        return false;
    }
}
