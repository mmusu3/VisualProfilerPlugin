using System;
using System.Collections.Generic;
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

    static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> instructions)
    {
        Plugin.Log.Debug($"Patching DelegateWork.Options setter.");

        var optionsField = typeof(AbstractWork).GetField("m_options", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fillDebugInfoMethod = typeof(AbstractWork).GetNonPublicInstanceMethod("FillDebugInfo", [ typeof(WorkOptions).MakeByRefType() ]);

        bool patched = false;

        foreach (var ins in instructions)
        {
            if (ins.OpCode == OpCodes.Ret)
            {
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Ldflda).InlineValue(optionsField);
                yield return new MsilInstruction(OpCodes.Callvirt).InlineValue(fillDebugInfoMethod);

                patched = true;
            }

            yield return ins;
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch DelegateWork.Options setter.");
    }
}
