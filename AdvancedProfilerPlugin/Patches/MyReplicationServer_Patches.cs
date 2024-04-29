using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyReplicationServer_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyReplicationServer).GetPublicInstanceMethod(nameof(MyReplicationServer.UpdateBefore));
        var prefix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateBefore));
        var suffix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Suffix_UpdateBefore));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        // TODO: Add more blocks under SendUpdate
        source = typeof(MyReplicationServer).GetPublicInstanceMethod(nameof(MyReplicationServer.SendUpdate));
        prefix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Prefix_SendUpdate));
        suffix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Suffix_SendUpdate));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyReplicationServer).GetPublicInstanceMethod(nameof(MyReplicationServer.ReplicableReady));
        prefix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Prefix_ReplicableReady));
        suffix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Suffix_ReplicableReady));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyReplicationServer).GetPublicInstanceMethod(nameof(MyReplicationServer.ReplicableRequest));
        prefix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Prefix_ReplicableRequest));
        suffix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod(nameof(Suffix_ReplicableRequest));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateBefore(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.UpdateBefore");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateBefore(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SendUpdate(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.SendUpdate");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SendUpdate(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ReplicableReady(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.ReplicableReady");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ReplicableReady(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ReplicableRequest(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.ReplicableRequest");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ReplicableRequest(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
