using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyNetworkWriter_SendAll_Patch
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyNetworkWriter).GetPublicStaticMethod(nameof(MyNetworkWriter.SendAll));
        var prefix = typeof(MyNetworkWriter_SendAll_Patch).GetNonPublicStaticMethod(nameof(Prefix_SendAll));
        var suffix = typeof(MyNetworkWriter_SendAll_Patch).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey SendAll;

        internal static void Init()
        {
            SendAll = ProfilerKeyCache.GetOrAdd("MyNetworkWriter.SendAll");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SendAll(ref ProfilerTimer __local_timer, ConcurrentQueue<MyNetworkWriter.MyPacketDescriptor> __field_m_packetsToSend)
    {
        __local_timer = Profiler.Start(Keys.SendAll, ProfilerTimerOptions.ProfileMemory, new(__field_m_packetsToSend.Count, "Packets to send: {0}"));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }
}
