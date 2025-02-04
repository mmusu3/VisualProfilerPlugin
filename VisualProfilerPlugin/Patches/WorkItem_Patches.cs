﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ParallelTasks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using static VisualProfiler.TranspileHelper;

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
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(WorkItem)}.{nameof(WorkItem.DoWork)}.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var taskStartedMethod = typeof(WorkItem_Patches).GetNonPublicStaticMethod(nameof(OnTaskStarted));
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
                    e.Emit(new(OpCodes.Ldarg_0));
                    e.Call(taskStartedMethod);
                    e.StoreLocal(timerLocal);
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ldloc_0 && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call2)
            {
                if (call2.Value == stackPopMethod)
                {
                    e.EmitDisposeProfilerTimer(timerLocal)[0].SwapTryCatchOperations(ref ins);
                    patchedParts++;
                }
            }

            e.Emit(ins);
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
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(WorkItem)}.{nameof(WorkItem.Wait)}.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Endfinally)
            {
                e.EmitStopProfilerTimer(timerLocal)[0].SwapTryCatchOperations(ref ins);
                patchedParts++;
            }

            e.Emit(ins);

            if (ins.OpCode == OpCodes.Nop && instructions[i - 1].OpCode == OpCodes.Ret)
            {
                e.EmitProfilerStart(Keys.WaitTask, ProfilerTimerOptions.ProfileMemory); // OnTaskStarted(MyProfiler.TaskType.SyncWait, "WaitTask");
                e.StoreLocal(timerLocal);
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
