using System.Collections.Generic;
using Torch.Managers.PatchManager.MSIL;

namespace AdvancedProfiler;

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
}
