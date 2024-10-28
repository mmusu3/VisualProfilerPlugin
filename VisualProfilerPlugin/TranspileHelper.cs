using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler;

static class TranspileHelper
{
    public static MsilInstruction WithTryCatchOperations(this MsilInstruction instruction, IEnumerable<MsilTryCatchOperation> operations)
    {
        instruction.TryCatchOperations.AddRange(operations);
        return instruction;
    }

    public static MsilInstruction SwapTryCatchOperations(this MsilInstruction instruction, MsilInstruction sourceInstruction)
    {
        instruction.TryCatchOperations.AddRange(sourceInstruction.TryCatchOperations);
        sourceInstruction.TryCatchOperations.Clear();

        return instruction;
    }

    public static MsilInstruction SwapLabels(this MsilInstruction instruction, MsilInstruction sourceInstruction)
    {
        foreach (var label in sourceInstruction.Labels)
            instruction.Labels.Add(label);

        sourceInstruction.Labels.Clear();

        return instruction;
    }

    public static MsilInstruction SwapLabelsAndTryCatchOperations(this MsilInstruction instruction, MsilInstruction sourceInstruction)
    {
        foreach (var label in sourceInstruction.Labels)
            instruction.Labels.Add(label);

        sourceInstruction.Labels.Clear();

        instruction.TryCatchOperations.AddRange(sourceInstruction.TryCatchOperations);
        sourceInstruction.TryCatchOperations.Clear();

        return instruction;
    }

    public static bool MatchOpCodes(MsilInstruction[] instructions, int start, OpCode[] opcodes)
    {
        if (instructions.Length < start + opcodes.Length)
            return false;

        for (int i = 0; i < opcodes.Length; i++)
        {
            if (instructions[start + i].OpCode != opcodes[i])
                return false;
        }

        return true;
    }

    public static MsilInstruction LoadString(string str) => new MsilInstruction(OpCodes.Ldstr).InlineValue(str);
    public static MsilInstruction LoadField(FieldInfo field) => new MsilInstruction(OpCodes.Ldfld).InlineValue(field);
    public static MsilInstruction Call(MethodInfo method) => new MsilInstruction(OpCodes.Call).InlineValue(method);
    public static MsilInstruction CallVirt(MethodInfo method) => new MsilInstruction(OpCodes.Callvirt).InlineValue(method);
    public static MsilInstruction NewObj(ConstructorInfo ctor) => new MsilInstruction(OpCodes.Newobj).InlineValue(ctor);
}
