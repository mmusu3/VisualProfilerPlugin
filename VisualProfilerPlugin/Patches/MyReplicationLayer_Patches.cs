using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyReplicationLayer_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(MyReplicationLayer).GetPublicInstanceMethod(nameof(MyReplicationLayer.OnEvent));
        var prefix = typeof(MyReplicationLayer_Patches).GetNonPublicStaticMethod(nameof(Prefix_OnEvent));
        var suffix = typeof(MyReplicationLayer_Patches).GetNonPublicStaticMethod(nameof(Suffix_OnEvent));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyReplicationLayer).GetPublicInstanceMethod(nameof(MyReplicationLayer.Invoke));
        prefix = typeof(MyReplicationLayer_Patches).GetNonPublicStaticMethod(nameof(Prefix_Invoke));
        suffix = typeof(MyReplicationLayer_Patches).GetNonPublicStaticMethod(nameof(Suffix_Invoke));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey OnEvent;
        internal static ProfilerKey Invoke;

        internal static void Init()
        {
            OnEvent = ProfilerKeyCache.GetOrAdd("MyReplicationLayer.OnEvent");
            Invoke = ProfilerKeyCache.GetOrAdd("MyReplicationLayer.Invoke");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_OnEvent(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.OnEvent); return true; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_OnEvent(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Invoke(ref ProfilerTimer __local_timer1, ref ProfilerTimer __local_timer2, VRage.Network.CallSite callSite)
    {
        __local_timer1 = Profiler.Start(Keys.Invoke);
        __local_timer2 = Profiler.Start(callSite.MethodInfo.Name);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Invoke(ref ProfilerTimer __local_timer1, ref ProfilerTimer __local_timer2)
    {
        __local_timer2.Stop();
        __local_timer1.Stop();
    }
}
