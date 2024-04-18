﻿using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ParallelTasks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class AbstractWork_Options_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(AbstractWork).GetProperty(nameof(AbstractWork.Options), BindingFlags.Public | BindingFlags.Instance)!.SetMethod!;
        var target = typeof(AbstractWork_Options_Patch).GetNonPublicStaticMethod(nameof(Transpile));

        ctx.GetPattern(source).Transpilers.Add(target);
    }

    static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> instructions)
    {
        Plugin.Log.Debug($"Patching {nameof(AbstractWork)}.{nameof(AbstractWork.Options)} setter.");

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
            Plugin.Log.Error($"Failed to patch {nameof(AbstractWork)}.{nameof(AbstractWork.Options)} setter.");
    }
}
