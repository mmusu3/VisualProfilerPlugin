using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
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

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyEntityCreationThread)}.ThreadProc.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var prefixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Prefix_ThreadProc));
        var suffixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Suffix_ThreadProc));

        var initEntityMethod = typeof(MyEntities).GetPublicStaticMethod(nameof(MyEntities.InitEntity));

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        ReadOnlySpan<OpCode> pattern1 = [OpCodes.Ldloca_S, OpCodes.Ldloc_0, OpCodes.Ldfld, OpCodes.Call];
        ReadOnlySpan<OpCode> pattern2 = [OpCodes.Ldloc_0, OpCodes.Ldfld, OpCodes.Ldloca_S, OpCodes.Ldflda, OpCodes.Ldc_I4_0, OpCodes.Call];

        Emit(Call(prefixMethod));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (MatchOpCodes(instructions, i, pattern2))
            {
                if (instructions[i + 5].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    e.EmitProfilerStart(1, "MyEntities.InitEntity");
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
                && ins.OpCode == OpCodes.Pop
                && instructions[i - 1].OpCode == OpCodes.Call)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    e.EmitStopProfilerTimer(timerLocal);
                    patchedParts++;
                }
            }
        }

        Emit(Call(suffixMethod));
        Emit(new(OpCodes.Ret));

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
