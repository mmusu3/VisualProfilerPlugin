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
        Plugin.Log.Debug($"Patching {nameof(MyEntityCreationThread)}.ThreadProc.");

        const int expectedParts = 4;
        int patchedParts = 0;

        var prefixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Prefix_ThreadProc));
        var suffixMethod = typeof(MyEntityCreationThread_Patches).GetNonPublicStaticMethod(nameof(Suffix_ThreadProc));
        var startMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(int), typeof(string)]);
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));

        var createNoInitMethod = typeof(MyEntities).GetPublicStaticMethod(nameof(MyEntities.CreateFromObjectBuilderNoinit));
        var initEntityMethod = typeof(MyEntities).GetPublicStaticMethod(nameof(MyEntities.InitEntity));

        var instructions = instructionStream.ToArray();
        var timerLocal = __localCreator(typeof(ProfilerTimer));

        yield return new MsilInstruction(OpCodes.Call).InlineValue(prefixMethod);

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (i < instructions.Length - 4
                && ins.OpCode == OpCodes.Ldloca_S
                && instructions[i + 1].OpCode == OpCodes.Ldloc_0
                && instructions[i + 2].OpCode == OpCodes.Ldfld
                && instructions[i + 3].OpCode == OpCodes.Call)
            {
                if (instructions[i + 3].Operand is MsilOperandInline<MethodBase> call && call.Value == createNoInitMethod)
                {
                    yield return new MsilInstruction(OpCodes.Ldc_I4_0);
                    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("MyEntities.CreateFromObjectBuilderNoinit");
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod);
                    yield return timerLocal.AsValueStore();
                    patchedParts++;
                }
            }
            else if (i < instructions.Length - 6
                && ins.OpCode == OpCodes.Ldloc_0
                && instructions[i + 1].OpCode == OpCodes.Ldfld
                && instructions[i + 2].OpCode == OpCodes.Ldloca_S
                && instructions[i + 3].OpCode == OpCodes.Ldflda
                && instructions[i + 4].OpCode == OpCodes.Ldc_I4_0
                && instructions[i + 5].OpCode == OpCodes.Call)
            {
                if (instructions[i + 5].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    yield return new MsilInstruction(OpCodes.Ldc_I4_1);
                    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("MyEntities.InitEntity");
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod);
                    yield return timerLocal.AsValueStore();
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            yield return ins;

            if (i > 1
                && ins.OpCode == OpCodes.Stfld
                && instructions[i - 1].OpCode == OpCodes.Call)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == createNoInitMethod)
                {
                    yield return timerLocal.AsValueLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
                    patchedParts++;
                }
            }
            else if (i > 1
                && ins.OpCode == OpCodes.Pop
                && instructions[i - 1].OpCode == OpCodes.Call)
            {
                if (instructions[i - 1].Operand is MsilOperandInline<MethodBase> call && call.Value == initEntityMethod)
                {
                    yield return timerLocal.AsValueLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
                    patchedParts++;
                }
            }
        }

        yield return new MsilInstruction(OpCodes.Call).InlineValue(suffixMethod);
        yield return new MsilInstruction(OpCodes.Ret);

        if (patchedParts != expectedParts)
            Plugin.Log.Fatal($"Failed to patch {nameof(MyEntityCreationThread)}.ThreadProc. {patchedParts} out of {expectedParts} code parts matched.");
        else
            Plugin.Log.Debug("Patch successful.");
    }
}
