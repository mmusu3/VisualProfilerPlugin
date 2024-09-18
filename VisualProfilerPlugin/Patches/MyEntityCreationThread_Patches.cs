using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

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

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyEntityCreationThread)}.ThreadProc.");

        const int expectedParts = 4;
        int patchedParts = 0;

        var prefixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Prefix_ThreadProc));
        var suffixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Suffix_ThreadProc));
        var startMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(int), typeof(string)]);
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));

        var createNoInitMethod = typeof(MyEntities).GetPublicStaticMethod(nameof(MyEntities.CreateFromObjectBuilderNoinit));
        var initEntityMethod = typeof(MyEntities).GetPublicStaticMethod(nameof(MyEntities.InitEntity));

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        var pattern1 = new[] { OpCodes.Ldloca_S, OpCodes.Ldloc_0, OpCodes.Ldfld, OpCodes.Call };
        var pattern2 = new[] { OpCodes.Ldloc_0, OpCodes.Ldfld, OpCodes.Ldloca_S, OpCodes.Ldflda, OpCodes.Ldc_I4_0, OpCodes.Call };

        Emit(new MsilInstruction(OpCodes.Call).InlineValue(prefixMethod));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (TranspileHelper.MatchOpCodes(instructions, i, pattern1))
            {
                if (instructions[i + 3].Operand is MsilOperandInline<MethodBase> call && call.Value == createNoInitMethod)
                {
                    Emit(new MsilInstruction(OpCodes.Ldc_I4_0));
                    Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("MyEntities.CreateFromObjectBuilderNoinit"));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod));
                    Emit(timerLocal.AsValueStore());
                    patchedParts++;
                }
            }
            else if (TranspileHelper.MatchOpCodes(instructions, i, pattern2))
            {
                if (instructions[i + 5].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    Emit(new MsilInstruction(OpCodes.Ldc_I4_1));
                    Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("MyEntities.InitEntity"));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod));
                    Emit(timerLocal.AsValueStore());
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            Emit(ins);

            if (i > 1
                && ins.OpCode == OpCodes.Stfld
                && instructions[i - 1].OpCode == OpCodes.Call)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == createNoInitMethod)
                {
                    Emit(timerLocal.AsValueLoad());
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(stopMethod));
                    patchedParts++;
                }
            }
            else if (i > 1
                && ins.OpCode == OpCodes.Pop
                && instructions[i - 1].OpCode == OpCodes.Call)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    Emit(timerLocal.AsValueLoad());
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(stopMethod));
                    patchedParts++;
                }
            }
        }

        Emit(new MsilInstruction(OpCodes.Call).InlineValue(suffixMethod));
        Emit(new MsilInstruction(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyEntityCreationThread)}.ThreadProc. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }
}
