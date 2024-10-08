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
    static Stack<bool>? state;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Begin(string key, MySimpleProfiler.ProfilingBlockType type)
    {
        var s = state ??= [];

        if (type == MySimpleProfiler.ProfilingBlockType.MOD)
        {
            Profiler.Start(key);
            s.Push(true);
        }
        else
        {
            s.Push(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_End()
    {
        var s = state;

        if (s != null && s.Pop())
            Profiler.Stop();

        return true;
    }
}
