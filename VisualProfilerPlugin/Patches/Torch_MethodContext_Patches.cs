using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Torch.Managers.PatchManager.MSIL;
using static VisualProfiler.TranspileHelper;

namespace VisualProfiler.Patches;

// Fix exception in Torch code patcher
// TODO: Submit fix to Torch
static class Torch_MethodContext_Patches
{
    public static void Patch()
    {
        var targetType = Type.GetType("Torch.Managers.PatchManager.Transpile.MethodContext, Torch")!;
        var source = targetType.GetNonPublicInstanceMethod("AddEhHandler");
        var originalIL = source.GetMethodBody()!.GetILAsByteArray()!;

        uint hash;

        using (var md5 = MD5.Create())
            hash = BitConverter.ToUInt32(md5.ComputeHash(originalIL), 0);

        if (hash != 605020245)
            return;

        Plugin.Log.Info("Begining early patch of MethodContext.AddEhHandler");

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
        var e = newInstructions;

        Plugin.Log.Debug($"Patching MethodContext.AddEhHandler.");

        var targetType = Type.GetType("Torch.Managers.PatchManager.Transpile.MethodContext, Torch")!;
        var findInstructionMethod = targetType.GetPublicInstanceMethod("FindInstruction");
        var instructionsField = targetType.GetField("_instructions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var eomIfNullMethod = typeof(Torch_MethodContext_Patches).GetNonPublicStaticMethod("GetEndOfMethodNopIfNull");

        const int expectedParts = 1;
        int patchedParts = 0;

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            e.Emit(ins);

            if (ins.OpCode == OpCodes.Call && ins.Operand is MsilOperandInline<MethodBase> call && call.Value == findInstructionMethod)
            {
                e.Emit(new(OpCodes.Ldarg_1));
                e.Emit(new(OpCodes.Ldarg_0));
                e.Emit(LoadField(instructionsField));
                e.Emit(Call(eomIfNullMethod));
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
