﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using static System.Reflection.Emit.OpCodes;
using static VisualProfiler.TranspileHelper;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyEntityCreationThread_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyEntityCreationThread).GetNonPublicInstanceMethod("ThreadProc");
        var transpiler = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Transpile_ThreadProc));

        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Prefix_ThreadProc()
    {
        Profiler.SetSortingGroupForCurrentThread("Async");
        Profiler.SetSortingGroupOrderPriority("Async", 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ThreadProc()
    {
        Profiler.RemoveGroupForCurrentThread();
    }

    static IEnumerable<MsilInstruction> Transpile_ThreadProc(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(MyEntityCreationThread)}.ThreadProc.");

        const int expectedParts = 3;
        int patchedParts = 0;

        var prefixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Prefix_ThreadProc));
        var suffixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Suffix_ThreadProc));

        var threadNameSetter = typeof(Thread).GetProperty(nameof(Thread.Name))!.SetMethod!;
        var initEntityMethod = typeof(MyEntities).GetPublicStaticMethod(nameof(MyEntities.InitEntity));

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        ReadOnlySpan<OpCode> pattern1 = [Ldloca_S, Ldloc_0, Ldfld, OpCodes.Call];
        ReadOnlySpan<OpCode> pattern2 = [Ldloc_0, Ldfld, Ldloca_S, Ldflda, Ldc_I4_0, OpCodes.Call];

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (MatchOpCodes(instructions, i, pattern2))
            {
                if (instructions[i + 5].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    e.EmitProfilerStart(1, "MyEntities.InitEntity");
                    e.StoreLocal(timerLocal);
                    patchedParts++;
                }
            }
            else if (ins.OpCode == Ret)
            {
                break;
            }

            e.Emit(ins);

            if (ins.OpCode == Callvirt && ins.Operand is MsilOperandInline<MethodBase> call1 && call1.Value == threadNameSetter)
            {
                e.Call(prefixMethod);
                patchedParts++;
            }
            else if (i > 1
                && ins.OpCode == Pop
                && instructions[i - 1].OpCode == OpCodes.Call)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    e.EmitStopProfilerTimer(timerLocal);
                    patchedParts++;
                }
            }
        }

        e.Call(suffixMethod);
        e.Emit(new(Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(MyEntityCreationThread)}.ThreadProc. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }
}
