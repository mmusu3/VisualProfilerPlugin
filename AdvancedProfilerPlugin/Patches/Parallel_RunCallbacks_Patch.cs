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

[PatchShim]
static class Parallel_RunCallbacks_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(Parallel).GetPublicStaticMethod(nameof(Parallel.RunCallbacks));
        var transpiler = typeof(Parallel_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(Transpile));

        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        Plugin.Log.Debug($"Patching {nameof(Parallel)}.{nameof(Parallel.RunCallbacks)}.");

        const int expectedParts = 4;
        int patchedParts = 0;

        var profilerStartMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [ typeof(string) ]);
        var startCallbackMethod = typeof(Parallel_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(StartCallback));
        var startDataCallbackMethod = typeof(Parallel_RunCallbacks_Patch).GetNonPublicStaticMethod(nameof(StartDataCallback));
        var profilerStopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));
        var profilerDisposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose));

        var invokeMethod = typeof(Action).GetPublicInstanceMethod(nameof(Action.Invoke));
        var getCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.Callback))!.GetMethod!;
        var setCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.Callback))!.SetMethod!;
        var getDataCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.DataCallback))!.GetMethod!;
        var setDataCallbackMethod = typeof(WorkItem).GetProperty(nameof(WorkItem.DataCallback))!.SetMethod!;

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Parallel.RunCallbacks");
        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod);
        yield return timerLocal1.AsValueStore();

        var instructions = instructionStream.ToArray();

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldloc_1 && instructions[i + 1].OpCode == OpCodes.Callvirt && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call1)
            {
                if (call1.Value == getCallbackMethod)
                {
                    if (instructions[i + 2].Operand is MsilOperandInline<MethodBase> call2 && call2.Value == invokeMethod)
                    {
                        yield return new MsilInstruction(OpCodes.Ldloc_1);
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(startCallbackMethod);
                        yield return timerLocal2.AsValueStore();
                        patchedParts++;
                    }
                }
                else if (call1.Value == getDataCallbackMethod)
                {
                    if (instructions[i + 2].OpCode == OpCodes.Ldloc_1)
                    {
                        yield return new MsilInstruction(OpCodes.Ldloc_1);
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(startDataCallbackMethod);
                        yield return timerLocal2.AsValueStore();
                        patchedParts++;
                    }
                }
            }

            if (ins.OpCode == OpCodes.Ret)
                break;

            yield return ins;

            if (ins.OpCode == OpCodes.Callvirt && ins.Operand is MsilOperandInline<MethodBase> call3)
            {
                if (call3.Value == setCallbackMethod || call3.Value == setDataCallbackMethod)
                {
                    yield return timerLocal2.AsValueLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerDisposeMethod);
                    patchedParts++;
                }
            }
        }

        yield return timerLocal1.AsValueLoad();
        yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
        yield return new MsilInstruction(OpCodes.Ret);

        if (patchedParts != expectedParts)
            Plugin.Log.Error($"Failed to patch {nameof(Parallel)}.{nameof(Parallel.RunCallbacks)}. {patchedParts} out of {expectedParts} code parts matched.");
        else
            Plugin.Log.Debug("Patch successful.");
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
