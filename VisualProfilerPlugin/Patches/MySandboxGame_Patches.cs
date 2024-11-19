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

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.LoadData.");

        bool patched = false;

        var baseSystemInitMethod = typeof(HkBaseSystem).GetPublicStaticMethod(nameof(HkBaseSystem.Init),
            [ typeof(int), typeof(Action<string>), typeof(bool), typeof(VRage.Library.Threading.ISharedCriticalSection) ]);

        var initProfilingMethod = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(InitHavokProfiling));

        foreach (var ins in instructions)
        {
            Emit(ins);

            if (ins.OpCode == OpCodes.Call && ins.Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == baseSystemInitMethod)
            {
                Emit(new MsilInstruction(OpCodes.Call).InlineValue(initProfilingMethod));
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

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.ProcessInvoke.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var profilerKeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null);
        var profilerEventExtraDataCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null);
        var startMethod1 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)]);
        var startMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string)]);
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));

        var invokeQueueField = typeof(MySandboxGame).GetField("m_invokeQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        var invokeDataType = typeof(MySandboxGame).GetNestedType("MyInvokeData", BindingFlags.NonPublic)!;
        var countGetter = typeof(MyConcurrentQueue<>).MakeGenericType(invokeDataType).GetProperty("Count", BindingFlags.Instance | BindingFlags.Public)!.GetMethod;
        var actionField = invokeDataType.GetField("Action", BindingFlags.Instance | BindingFlags.Public);
        var invokerField = invokeDataType.GetField("Invoker", BindingFlags.Instance | BindingFlags.Public);
        var invokeMethod = typeof(Action<object>).GetPublicInstanceMethod(nameof(Action<object>.Invoke));

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue(Keys.ProcessInvoke.GlobalIndex));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerKeyCtor));
        Emit(new MsilInstruction(OpCodes.Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
        Emit(new MsilInstruction(OpCodes.Ldarg_0));
        Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(invokeQueueField));
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(countGetter));
        Emit(new MsilInstruction(OpCodes.Conv_I8));
        Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Queue Count: {0}"));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerEventExtraDataCtor)); // new ProfilerEvent.ExtraData(count, format)
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod1));
        Emit(timerLocal1.AsValueStore());

        ReadOnlySpan<OpCode> pattern1 = [OpCodes.Ldloc_0, OpCodes.Ldfld, OpCodes.Brfalse_S];
        ReadOnlySpan<OpCode> pattern2 = [OpCodes.Callvirt, OpCodes.Ldloc_0];

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (TranspileHelper.MatchOpCodes(instructions, i, pattern1))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<FieldInfo> ldField && ldField.Value == actionField)
                {
                    Emit(new MsilInstruction(OpCodes.Ldloc_0).SwapLabels(ref ins));
                    Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(invokerField));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod2));
                    Emit(timerLocal2.AsValueStore());
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            Emit(ins);

            if (TranspileHelper.MatchOpCodes(instructions, i, pattern2)
                && ins.Operand is MsilOperandInline<MethodBase> call && call.Value == invokeMethod)
            {
                var nextIns = instructions[++i];
                Emit(timerLocal2.AsValueLoad().SwapLabels(ref nextIns));
                Emit(new MsilInstruction(OpCodes.Call).InlineValue(stopMethod));
                Emit(nextIns);
                patchedParts++;
            }
        }

        Emit(timerLocal1.AsValueLoad());
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(stopMethod));
        Emit(new MsilInstruction(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MySandboxGame)}.ProcessInvoke. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }
}
