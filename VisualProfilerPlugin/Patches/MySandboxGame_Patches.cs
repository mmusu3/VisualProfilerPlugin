using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Havok;
using Sandbox;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Collections;
using VRage.Profiler;
using static VisualProfiler.TranspileHelper;

namespace VisualProfiler.Patches;

[PatchShim]
static class MySandboxGame_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MySandboxGame).GetPublicInstanceMethod("Run");
        var prefix = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Prefix_Run));

        ctx.GetPattern(source).Prefixes.Add(prefix);

        source = typeof(MySandboxGame).GetNonPublicInstanceMethod("LoadData");
        var transpiler = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Transpile_LoadData));

        ctx.GetPattern(source).Transpilers.Add(transpiler);

        source = typeof(MySandboxGame).GetPublicInstanceMethod(nameof(MySandboxGame.ProcessInvoke));
        transpiler = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Transpile_ProcessInvoke));

        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    static class Keys
    {
        internal static ProfilerKey ProcessInvoke;

        internal static void Init()
        {
            ProcessInvoke = ProfilerKeyCache.GetOrAdd("MySandboxGame.ProcessInvoke");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Run()
    {
        Profiler.SetSortingGroupForCurrentThread("Main");
        Profiler.SetSortingGroupOrderPriority("Main", 100);
        Profiler.SetIsRealtimeThread(true);
        return true;
    }

    static IEnumerable<MsilInstruction> Transpile_LoadData(IEnumerable<MsilInstruction> instructionStream)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.LoadData.");

        bool patched = false;

        var baseSystemInitMethod = typeof(HkBaseSystem).GetPublicStaticMethod(nameof(HkBaseSystem.Init),
            [ typeof(int), typeof(Action<string>), typeof(bool), typeof(VRage.Library.Threading.ISharedCriticalSection) ]);

        var initProfilingMethod = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(InitHavokProfiling));

        foreach (var ins in instructions)
        {
            e.Emit(ins);

            if (ins.OpCode == OpCodes.Call && ins.Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == baseSystemInitMethod)
            {
                e.Call(initProfilingMethod);
                patched = true;
            }
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MySandboxGame)}.LoadData.");

        return patched ? newInstructions : instructions;
    }

    static string[] taskNames = null!;
    static Delegate taskStartedFuncCpp = null!;
    static HkTaskProfiler.TaskFinishedFunc taskFinishedFuncCpp = null!;

    static void InitHavokProfiling()
    {
        var taskNames = new string[HkTaskType.HK_JOB_TYPE_OTHER + 1 - HkTaskType.Schedule];

        for (int i = (int)HkTaskType.Schedule; i <= (int)HkTaskType.HK_JOB_TYPE_OTHER; i++)
            taskNames[i - (int)HkTaskType.Schedule] = ((MyProfiler.TaskType)i).ToString();

        MySandboxGame_Patches.taskNames = taskNames;

        var cppDelegateType = typeof(HkTaskProfiler).GetNestedType("TaskStartedFuncCpp", BindingFlags.NonPublic)!;

        taskStartedFuncCpp = Delegate.CreateDelegate(cppDelegateType, typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(TaskStarted)));
        taskFinishedFuncCpp = Profiler.Stop;

        typeof(HkTaskProfiler).GetNonPublicStaticMethod("HkTaskProfiler_Init").Invoke(null, [taskStartedFuncCpp, taskFinishedFuncCpp]);
    }

    static unsafe void TaskStarted(char* _, HkTaskType type)
    {
        int index = type - HkTaskType.Schedule;
        var taskName = (uint)index < (uint)taskNames.Length ? taskNames[index] : "UnknownTask";

        Profiler.Start(type - HkTaskType.Schedule, taskName);
    }

    static IEnumerable<MsilInstruction> Transpile_ProcessInvoke(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.ProcessInvoke.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var invokeQueueField = typeof(MySandboxGame).GetField("m_invokeQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var invokeDataType = typeof(MySandboxGame).GetNestedType("MyInvokeData", BindingFlags.NonPublic)!;
        var countGetter = typeof(MyConcurrentQueue<>).MakeGenericType(invokeDataType).GetProperty("Count", BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;
        var actionField = invokeDataType.GetField("Action", BindingFlags.Instance | BindingFlags.Public)!;
        var invokerField = invokeDataType.GetField("Invoker", BindingFlags.Instance | BindingFlags.Public)!;
        var invokeMethod = typeof(Action<object>).GetPublicInstanceMethod(nameof(Action<object>.Invoke));

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        e.EmitProfilerStartLongExtra(Keys.ProcessInvoke, ProfilerTimerOptions.ProfileMemory, "Queue Count: {0}", [
            new(OpCodes.Ldarg_0),
            LoadField(invokeQueueField),
            Call(countGetter),
            new(OpCodes.Conv_I8)
        ]);
        e.StoreLocal(timerLocal1);

        ReadOnlySpan<OpCode> pattern1 = [OpCodes.Ldloc_0, OpCodes.Ldfld, OpCodes.Brfalse_S];
        ReadOnlySpan<OpCode> pattern2 = [OpCodes.Callvirt, OpCodes.Ldloc_0];

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (MatchOpCodes(instructions, i, pattern1))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<FieldInfo> ldField && ldField.Value == actionField)
                {
                    e.EmitProfilerStartName([
                        LoadLocal(0).SwapLabels(ref ins),
                        LoadField(invokerField)
                    ]);
                    e.StoreLocal(timerLocal2);
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            e.Emit(ins);

            if (MatchOpCodes(instructions, i, pattern2)
                && ins.Operand is MsilOperandInline<MethodBase> call && call.Value == invokeMethod)
            {
                var nextIns = instructions[++i];
                e.EmitStopProfilerTimer(timerLocal2)[0].SwapLabels(ref nextIns);
                e.Emit(nextIns);
                patchedParts++;
            }
        }

        e.EmitStopProfilerTimer(timerLocal1);
        e.Emit(new(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(MySandboxGame)}.ProcessInvoke. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }
}
