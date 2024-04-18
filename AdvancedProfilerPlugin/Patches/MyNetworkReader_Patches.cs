using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyNetworkReader_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = Type.GetType("Sandbox.Engine.Networking.MyNetworkReader, Sandbox.Game")!.GetPublicStaticMethod("Process");
        var prefix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Prefix_Process));
        var suffix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Suffix_Process));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = Type.GetType("Sandbox.Engine.Networking.MyNetworkReader, Sandbox.Game")!.GetPublicStaticMethod("ReceiveAll");
        prefix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Prefix_ReceiveAll));
        suffix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Suffix_ReceiveAll));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Process(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyNetworkReader.Process");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Process(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ReceiveAll(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyNetworkReader.ReceiveAll");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ReceiveAll(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
