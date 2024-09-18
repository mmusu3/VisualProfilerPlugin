#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler.Patches;

// Fix Torch exception
static class Torch_MethodContext_Patches
{
    public static void Patch()
    {
        Plugin.Log.Info("Begining early patch of MethodContext.AddEhHandler");

        var targetType = Type.GetType("Torch.Managers.PatchManager.Transpile.MethodContext, Torch");
        var source = targetType.GetNonPublicInstanceMethod("AddEhHandler");
        var transpiler = typeof(Torch_MethodContext_Patches).GetNonPublicStaticMethod(nameof(Transpile_AddEhHandler));

        var pattern = PatchHelper.CreateRewritePattern(source);
        pattern.Transpilers.Add(transpiler);

        PatchHelper.CommitMethodPatches(pattern);

        Plugin.Log.Info("Early patch completed.");
    }

    static IEnumerable<MsilInstruction> Transpile_AddEhHandler(IEnumerable<MsilInstruction> instructionStream)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching MethodContext.AddEhHandler.");

        var targetType = Type.GetType("Torch.Managers.PatchManager.Transpile.MethodContext, Torch");
        var findInstructionMethod = targetType.GetPublicInstanceMethod("FindInstruction");
        var instructionsField = targetType.GetField("_instructions", BindingFlags.Instance | BindingFlags.NonPublic);
        var eomIfNullMethod = typeof(Torch_MethodContext_Patches).GetNonPublicStaticMethod("GetEndOfMethodNopIfNull");

        const int expectedParts = 1;
        int patchedParts = 0;

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            Emit(ins);

            if (ins.OpCode == OpCodes.Call && ins.Operand is MsilOperandInline<MethodBase> call && call.Value == findInstructionMethod)
            {
                Emit(new MsilInstruction(OpCodes.Ldarg_1));
                Emit(new MsilInstruction(OpCodes.Ldarg_0));
                Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(instructionsField));
                Emit(new MsilInstruction(OpCodes.Call).InlineValue(eomIfNullMethod));
                patchedParts++;
            }
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch MethodContext.AddEhHandler. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    static MsilInstruction? GetEndOfMethodNopIfNull(MsilInstruction? instruction, int offset, List<MsilInstruction> _instructions)
    {
        // If a catch block is at the end of a method that does not
        // return then there will be no instruction at the given offset.
        // Add a nop instruction as a workaround.
        if (instruction == null && offset == _instructions[^1].Offset + _instructions[^1].MaxBytes)
            _instructions.Add(instruction = new MsilInstruction(OpCodes.Nop));

        return instruction;
    }
}
#endif
