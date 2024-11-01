using System;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyProgrammableBlock_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyProgrammableBlock).GetPublicInstanceMethod(nameof(MyProgrammableBlock.RunSandboxedProgramAction));
        var prefix = typeof(MyProgrammableBlock_Patches).GetNonPublicStaticMethod(nameof(Prefix_RunSandboxedProgramAction));
        var suffix = typeof(MyProgrammableBlock_Patches).GetNonPublicStaticMethod(nameof(Suffix_RunSandboxedProgramAction));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey RunSandboxedProgramAction;

        internal static void Init()
        {
            RunSandboxedProgramAction = ProfilerKeyCache.GetOrAdd("MyProgrammableBlock.RunSandboxedProgramAction");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RunSandboxedProgramAction(ref ProfilerTimer __local_timer1, ref ProfilerTimer __local_timer2,
        MyProgrammableBlock __instance, Action<IMyGridProgram> action)
    {
        __local_timer1 = Profiler.Start(Keys.RunSandboxedProgramAction, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.Scripts, __instance));
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
