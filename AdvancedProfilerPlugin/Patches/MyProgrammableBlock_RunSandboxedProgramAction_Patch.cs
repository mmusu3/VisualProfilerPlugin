using System;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyProgrammableBlock_RunSandboxedProgramAction_Patch
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyProgrammableBlock).GetPublicInstanceMethod(nameof(MyProgrammableBlock.RunSandboxedProgramAction));
        var prefix = typeof(MyProgrammableBlock_RunSandboxedProgramAction_Patch).GetNonPublicStaticMethod(nameof(Prefix_RunSandboxedProgramAction));
        var suffix = typeof(MyProgrammableBlock_RunSandboxedProgramAction_Patch).GetNonPublicStaticMethod(nameof(Suffix_RunSandboxedProgramAction));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RunSandboxedProgramAction(ref ProfilerTimer __local_timer1, ref ProfilerTimer __local_timer2,
        MyProgrammableBlock __instance, Action<IMyGridProgram> action)
    {
        __local_timer1 = Profiler.Start("MyProgrammableBlock.RunSandboxedProgramAction", profileMemory: true, new(__instance));
        __local_timer2 = Profiler.Start(0, action.Method.Name);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_RunSandboxedProgramAction(ref ProfilerTimer __local_timer1, ref ProfilerTimer __local_timer2)
    {
        __local_timer2.Stop();
        __local_timer1.Stop();
    }
}
