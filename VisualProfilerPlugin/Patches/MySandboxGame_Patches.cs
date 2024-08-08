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

namespace VisualProfiler.Patches;

[PatchShim]
static class MySandboxGame_Patches
{
    public static void Patch(PatchContext ctx)
    {
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Run()
    {
        Profiler.SetSortingGroupForCurrentThread("Main");
        Profiler.SetSortingGroupOrderPriority("Main", 100);
        Profiler.SetIsRealtimeThread(true);
        return true;
    }

    static IEnumerable<MsilInstruction> Transpile_LoadData(IEnumerable<MsilInstruction> instructions)
    {
        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.LoadData.");

        bool patched = false;

        var baseSystemInitMethod = typeof(HkBaseSystem).GetPublicStaticMethod(nameof(HkBaseSystem.Init),
            [ typeof(int), typeof(Action<string>), typeof(bool), typeof(VRage.Library.Threading.ISharedCriticalSection) ]);

        var initProfilingMethod = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(InitHavokProfiling));

        foreach (var ins in instructions)
        {
            yield return ins;

            if (ins.OpCode == OpCodes.Call && ins.Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == baseSystemInitMethod)
            {
                yield return new MsilInstruction(OpCodes.Call).InlineValue(initProfilingMethod);
                patched = true;
            }
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MySandboxGame)}.LoadData.");
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
        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.ProcessInvoke.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var profilerEventExtraDataCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null);
        var startMethod1 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(string), typeof(bool), typeof(ProfilerEvent.ExtraData)]);
        var startMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(string)]);
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));

        var invokeQueueField = typeof(MySandboxGame).GetField("m_invokeQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        var invokeDataType = typeof(MySandboxGame).GetNestedType("MyInvokeData", BindingFlags.NonPublic)!;
        var countGetter = typeof(MyConcurrentQueue<>).MakeGenericType(invokeDataType).GetProperty("Count", BindingFlags.Instance | BindingFlags.Public)!.GetMethod;
        var actionField = invokeDataType.GetField("Action", BindingFlags.Instance | BindingFlags.Public);
        var invokerField = invokeDataType.GetField("Invoker", BindingFlags.Instance | BindingFlags.Public);
        var invokeMethod = typeof(Action<object>).GetPublicInstanceMethod(nameof(Action<object>.Invoke));

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("MySandboxGame.ProcessInvoke");
        yield return new MsilInstruction(OpCodes.Ldc_I4_1); // profileMemory: true
        yield return new MsilInstruction(OpCodes.Ldarg_0);
        yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(invokeQueueField);
        yield return new MsilInstruction(OpCodes.Call).InlineValue(countGetter);
        yield return new MsilInstruction(OpCodes.Conv_I8);
        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Queue Count: {0}");
        yield return new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor); // new ProfilerEvent.ExtraData(count, format)
        yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod1);
        yield return timerLocal1.AsValueStore();

        var instructions = instructionStream.ToArray();

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];
            var nextIns = i < instructions.Length - 1 ? instructions[i + 1] : null;

            if (ins.OpCode == OpCodes.Ldloc_0 && nextIns != null && nextIns.OpCode == OpCodes.Ldfld)
            {
                if (nextIns.Operand is MsilOperandInline<FieldInfo> ldField && ldField.Value == actionField)
                {
                    if (i < instructions.Length - 2 && instructions[i + 2].OpCode == OpCodes.Brfalse_S)
                    {
                        yield return new MsilInstruction(OpCodes.Ldloc_0).SwapLabels(ins);
                        yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(invokerField);
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod2);
                        yield return timerLocal2.AsValueStore();
                        patchedParts++;
                    }
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            yield return ins;

            if (ins.OpCode == OpCodes.Callvirt && ins.Operand is MsilOperandInline<MethodBase> call && call.Value == invokeMethod
                && nextIns != null && nextIns.OpCode == OpCodes.Ldloc_0)
            {
                yield return timerLocal2.AsValueLoad().SwapLabels(nextIns);
                yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
                patchedParts++;
            }
        }

        yield return timerLocal1.AsValueLoad();
        yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
        yield return new MsilInstruction(OpCodes.Ret);

        if (patchedParts != expectedParts)
            Plugin.Log.Fatal($"Failed to patch {nameof(MySandboxGame)}.ProcessInvoke. {patchedParts} out of {expectedParts} code parts matched.");
        else
            Plugin.Log.Debug("Patch successful.");
    }
}
