using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyReceiveQueue_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = Type.GetType("Sandbox.Engine.Networking.MyReceiveQueue, Sandbox.Game")!.GetPublicInstanceMethod("Process");
        var prefix = typeof(MyReceiveQueue_Patches).GetNonPublicStaticMethod(nameof(Prefix_Process));
        var suffix = typeof(MyReceiveQueue_Patches).GetNonPublicStaticMethod(nameof(Suffix_Process));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Process(ref ProfilerTimer __local_timer, ConcurrentQueue<MyPacket> __field_m_receiveQueue)
    {
        __local_timer = Profiler.Start("MyReceiveQueue.Process", profileMemory: true, new(__field_m_receiveQueue.Count, "ReceiveQueue Count: {0}"));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Process(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
