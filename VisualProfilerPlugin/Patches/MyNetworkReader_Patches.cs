using System;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyNetworkReader_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = Type.GetType("Sandbox.Engine.Networking.MyNetworkReader, Sandbox.Game")!.GetPublicStaticMethod("Process");
        var prefix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Prefix_Process));
        var suffix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = Type.GetType("Sandbox.Engine.Networking.MyNetworkReader, Sandbox.Game")!.GetPublicStaticMethod("ReceiveAll");
        prefix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Prefix_ReceiveAll));
        suffix = typeof(MyNetworkReader_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Process;
        internal static ProfilerKey ReceiveAll;

        internal static void Init()
        {
            Process = ProfilerKeyCache.GetOrAdd("MyNetworkReader.Process");
            ReceiveAll = ProfilerKeyCache.GetOrAdd("MyNetworkReader.ReceiveAll");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Process(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Process); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ReceiveAll(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ReceiveAll); return true; }
}
