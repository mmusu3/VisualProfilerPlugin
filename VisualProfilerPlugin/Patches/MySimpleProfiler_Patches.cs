using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage;

namespace VisualProfiler.Patches;

[PatchShim]
static class MySimpleProfiler_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MySimpleProfiler).GetPublicStaticMethod(nameof(MySimpleProfiler.Begin));
        var suffix = typeof(MySimpleProfiler_Patches).GetNonPublicStaticMethod(nameof(Suffix_Begin));

        ctx.GetPattern(source).Suffixes.Add(suffix);

        var prefix = typeof(MySimpleProfiler_Patches).GetNonPublicStaticMethod(nameof(Prefix_End));

        source = typeof(MySimpleProfiler).GetPublicStaticMethod(nameof(MySimpleProfiler.EndNoMemberPairingCheck));
        ctx.GetPattern(source).Prefixes.Add(prefix);

        source = typeof(MySimpleProfiler).GetPublicStaticMethod(nameof(MySimpleProfiler.EndMemberPairingCheck));
        ctx.GetPattern(source).Prefixes.Add(prefix);
    }

    [ThreadStatic]
    static Stack<ProfilerTimer?>? timerStack;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Begin(string key, MySimpleProfiler.ProfilingBlockType type)
    {
        var s = timerStack ??= [];

        if (type == MySimpleProfiler.ProfilingBlockType.MOD)
        {
            var timer = Profiler.Start(key, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.Mods));
            s.Push(timer);
        }
        else
        {
            s.Push(null);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_End()
    {
        var s = timerStack;

        if (s != null && s.Pop() is { } t)
            t.Stop();

        return true;
    }
}
