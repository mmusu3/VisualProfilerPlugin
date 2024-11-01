using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ParallelTasks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler.Patches;

[PatchShim]
static class WorkItem_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(WorkItem).GetPublicInstanceMethod(nameof(WorkItem.DoWork));
        var transpiler = typeof(WorkItem_Patches).GetNonPublicStaticMethod(nameof(Transpile_DoWork));

        ctx.GetPattern(source).Transpilers.Add(transpiler);

        source = typeof(WorkItem).GetPublicInstanceMethod(nameof(WorkItem.Wait));
        transpiler = typeof(WorkItem_Patches).GetNonPublicStaticMethod(nameof(Transpile_Wait));

        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    static class Keys
    {
        internal static ProfilerKey WaitTask;

        internal static void Init()
        {
            WaitTask = ProfilerKeyCache.GetOrAdd("WaitTask");
        }
    }

    static IEnumerable<MsilInstruction> Transpile_DoWork(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(WorkItem)}.{nameof(WorkItem.DoWork)}.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var taskStartedMethod = typeof(WorkItem_Patches).GetNonPublicStaticMethod(nameof(OnTaskStarted));
        var profilerDisposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose));
        var stackPushMethod = typeof(Stack<Task>).GetPublicInstanceMethod(nameof(Stack<Task>.Push));
        var stackPopMethod = typeof(Stack<Task>).GetPublicInstanceMethod(nameof(Stack<Task>.Pop));

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.Operand is MsilOperandInline<MethodBase> call1)
            {
                if (call1.Value == stackPushMethod)
                {
                    Emit(new MsilInstruction(OpCodes.Ldarg_0));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(taskStartedMethod));
                    Emit(timerLocal.AsValueStore());
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ldloc_0 && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call2)
            {
                if (call2.Value == stackPopMethod)
                {
                    Emit(timerLocal.AsValueLoad().SwapTryCatchOperations(ins));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerDisposeMethod));
                    patchedParts++;
                }
            }

            Emit(ins);
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(WorkItem)}.{nameof(WorkItem.DoWork)}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ProfilerTimer OnTaskStarted(WorkItem workItem)
    {
        var options = workItem.Work.Options;
        var name = options.DebugName ?? GetTaskName(workItem, options);

        return Profiler.Start(name);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static string GetTaskName(WorkItem workItem, WorkOptions options)
    {
        if (workItem.Work is AbstractWork absWork)
            options = absWork.Options = options;

        var name = options.DebugName;

        if (name != null)
            return name;

        var tt = options.TaskType;

        if (tt != VRage.Profiler.MyProfiler.TaskType.None
            && tt != VRage.Profiler.MyProfiler.TaskType.WorkItem)
        {
            name = tt.ToString();
        }
        else
        {
            name = workItem.Work.GetType().Name;
        }

        return name;
    }

    static IEnumerable<MsilInstruction> Transpile_Wait(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(WorkItem)}.{nameof(WorkItem.Wait)}.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var profilerKeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(ProfilerKey), typeof(ProfilerTimerOptions)], null);
        var profilerStartMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions)]);
        var profilerStopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Endfinally)
            {
                Emit(timerLocal.AsValueLoad().SwapTryCatchOperations(ins));
                Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod));
                patchedParts++;
            }

            Emit(ins);

            if (ins.OpCode == OpCodes.Nop && instructions[i - 1].OpCode == OpCodes.Ret)
            {
                Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue(Keys.WaitTask.GlobalIndex));
                Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerKeyCtor));
                Emit(new MsilInstruction(OpCodes.Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                Emit(new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod)); // OnTaskStarted(MyProfiler.TaskType.SyncWait, "WaitTask");
                Emit(timerLocal.AsValueStore());
                patchedParts++;
            }
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(WorkItem)}.{nameof(WorkItem.Wait)}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }
}
