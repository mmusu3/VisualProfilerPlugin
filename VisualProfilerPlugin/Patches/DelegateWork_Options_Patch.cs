using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ParallelTasks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler.Patches;

[PatchShim]
static class DelegateWork_Options_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = Type.GetType("ParallelTasks.DelegateWork, VRage.Library")!.GetProperty("Options", BindingFlags.Public | BindingFlags.Instance)!.SetMethod!;
        var target = typeof(DelegateWork_Options_Patch).GetNonPublicStaticMethod(nameof(Transpile));

        ctx.GetPattern(source).Transpilers.Add(target);
    }

    static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> instructionStream)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching DelegateWork.Options setter.");

        var optionsField = typeof(AbstractWork).GetField("m_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fillDebugInfoMethod = typeof(AbstractWork).GetNonPublicInstanceMethod("FillDebugInfo", [ typeof(WorkOptions).MakeByRefType() ]);

        bool patched = false;

        foreach (var ins in instructions)
        {
            if (ins.OpCode == OpCodes.Ret)
            {
                e.Emit(new(OpCodes.Ldarg_0));
                e.Emit(new(OpCodes.Ldarg_0));
                e.LoadFieldAddress(optionsField);
                e.CallVirt(fillDebugInfoMethod);

                patched = true;
            }

            e.Emit(ins);
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch DelegateWork.Options setter.");

        return patched ? newInstructions : instructions;
    }
}
