using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ParallelTasks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace AdvancedProfiler.Patches;

// Just doesn't want to work
//[PatchShim]
static class PrioritizedScheduler_Patches
{
    static MethodInfo scheduleMethod = null!;

    public static void Patch(PatchContext ctx)
    {
        var wokerArrayType = typeof(PrioritizedScheduler).GetNestedType("WorkerArray", BindingFlags.NonPublic)!;
        var source = wokerArrayType.GetPublicInstanceMethod("ScheduleOnEachWorker");
        //var transpiler = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(Transpile_WorkerArray_ScheduleOnEachWorker));

        //ctx.GetPattern(source).Transpilers.Add(transpiler);

        scheduleMethod = wokerArrayType.GetPublicInstanceMethod("Schedule");

        // Transpiler causes a wierd null ref issue even with a blank transpiler.
        var prefix = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(Prefix_ScheduleOnEachWorker));
        ctx.GetPattern(source).Prefixes.Add(prefix);
    }

    static IEnumerable<MsilInstruction> Transpile_WorkerArray_ScheduleOnEachWorker(IEnumerable<MsilInstruction> instructionStream)
    {
        Plugin.Log.Debug($"Patching {nameof(PrioritizedScheduler)}.WorkerArray.ScheduleOnEachWorker.");

        const int expectedParts = 1;
        int patchedParts = 0;

        var createOptionsMethod = typeof(PrioritizedScheduler_Patches).GetNonPublicStaticMethod(nameof(CreateWorkOptions));
        var defaultOptionsField = typeof(Parallel).GetField(nameof(Parallel.DefaultOptions), BindingFlags.Static | BindingFlags.Public);
        var workersGetter = typeof(PrioritizedScheduler).GetNestedType("WorkerArray", BindingFlags.NonPublic)!.GetProperty("Workers", BindingFlags.Instance | BindingFlags.Public)!.GetMethod;

        var instructions = instructionStream.ToArray();
        bool skipping = false;

        const int toSkip = 8;
        int skipped = 0;

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldsfld && ins.Operand is MsilOperandInline<FieldInfo> ldField && ldField.Value == defaultOptionsField)
            {
                yield return new MsilInstruction(OpCodes.Ldarg_1);
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Call).InlineValue(workersGetter);
                yield return new MsilInstruction(OpCodes.Ldlen);
                yield return new MsilInstruction(OpCodes.Conv_I4);
                yield return new MsilInstruction(OpCodes.Call).InlineValue(createOptionsMethod);
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
                yield return ins;
        }

        if (patchedParts != expectedParts)
            Plugin.Log.Error($"Failed to patch {nameof(PrioritizedScheduler)}.WorkerArray.ScheduleOnEachWorker. {patchedParts} out of {expectedParts} code parts matched.");
        else if (skipped != toSkip)
            Plugin.Log.Error($"Failed to patch {nameof(PrioritizedScheduler)}.WorkerArray.ScheduleOnEachWorker. {skipped} out of {toSkip} instructions skipped.");
        else
            Plugin.Log.Debug("Patch successful.");
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
        var barrier = new System.Threading.Barrier(__field_m_workers.Length);

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
