using System;
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
static class Parallel_RunCallbacks_Patch
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(Parallel).GetPublicStaticMethod(nameof(Parallel.RunCallbacks));
        var transpiler = typeof(Parallel_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(Transpile_RunCallbacks));

        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    static class Keys
    {
        internal static ProfilerKey RunCallbacks;

        internal static void Init()
        {
            RunCallbacks = ProfilerKeyCache.GetOrAdd("Parallel.RunCallbacks");
        }
    }

    static IEnumerable<MsilInstruction> Transpile_RunCallbacks(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(Parallel)}.{nameof(Parallel.RunCallbacks)}.");

        const int expectedParts = 4;
        int patchedParts = 0;

        var startCallbackMethod = typeof(Parallel_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(StartCallback));
        var startDataCallbackMethod = typeof(Parallel_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(StartDataCallback));

        var invokeMethod = typeof(Action).GetPublicInstanceMethod(nameof(Action.Invoke));
        var getCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.Callback))!.GetMethod!;
        var setCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.Callback))!.SetMethod!;
        var getDataCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.DataCallback))!.GetMethod!;
        var setDataCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.DataCallback))!.SetMethod!;

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        e.EmitProfilerStart(Keys.RunCallbacks, ProfilerTimerOptions.ProfileMemory);
        Emit(timerLocal1.AsValueStore());

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldloc_1 && instructions[i + 1].OpCode == OpCodes.Callvirt && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call1)
            {
                if (call1.Value == getCallbackMethod)
                {
                    if (instructions[i + 2].Operand is MsilOperandInline<MethodBase> call2 && call2.Value == invokeMethod)
                    {
                        Emit(new(OpCodes.Ldloc_1));
                        Emit(Call(startCallbackMethod));
                        Emit(timerLocal2.AsValueStore());
                        patchedParts++;
                    }
                }
                else if (call1.Value == getDataCallbackMethod)
                {
                    if (instructions[i + 2].OpCode == OpCodes.Ldloc_1)
                    {
                        Emit(new(OpCodes.Ldloc_1));
                        Emit(Call(startDataCallbackMethod));
                        Emit(timerLocal2.AsValueStore());
                        patchedParts++;
                    }
                }
            }

            if (ins.OpCode == OpCodes.Ret)
                break;

            Emit(ins);

            if (ins.OpCode == OpCodes.Callvirt && ins.Operand is MsilOperandInline<MethodBase> call3)
            {
                if (call3.Value == setCallbackMethod || call3.Value == setDataCallbackMethod)
                {
                    e.EmitDisposeProfilerTimer(timerLocal2);
                    patchedParts++;
                }
            }
        }

        e.EmitStopProfilerTimer(timerLocal1);
        Emit(new(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(Parallel)}.{nameof(Parallel.RunCallbacks)}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ProfilerTimer StartCallback(WorkItem workItem)
    {
        return Profiler.Start(workItem.Callback.Method.Name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ProfilerTimer StartDataCallback(WorkItem workItem)
    {
        return Profiler.Start(workItem.DataCallback.Method.Name);
    }
}
