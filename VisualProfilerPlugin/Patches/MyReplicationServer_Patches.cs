using System.Reflection;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyReplicationServer_Patches
{
    public static void Patch(PatchContext ctx)
    {
        PatchPrefixSuffixPair(ctx, nameof(MyReplicationServer.Destroy), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyReplicationServer.UpdateBefore), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyReplicationServer.SendUpdate), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, "RefreshReplicable", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "ApplyDirtyGroups", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "FilterStateSync", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "AddForClient", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "SendStreamingEntry", _public: false, _static: false);
        // Too spammy
        //PatchPrefixSuffixPair(ctx, "ScheduleStateGroupSync", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyReplicationServer.ReplicableReady), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyReplicationServer.ReplicableRequest), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        MethodInfo source;

        if (_public)
        {
            if (_static)
                source = typeof(MyReplicationServer).GetPublicStaticMethod(methodName);
            else
                source = typeof(MyReplicationServer).GetPublicInstanceMethod(methodName);
        }
        else
        {
            if (_static)
                source = typeof(MyReplicationServer).GetNonPublicStaticMethod(methodName);
            else
                source = typeof(MyReplicationServer).GetNonPublicInstanceMethod(methodName);
        }

        var prefix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod("Suffix_" + methodName);

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Destroy(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.Destroy");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Destroy(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
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
    static bool Prefix_RefreshReplicable(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.RefreshReplicable");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_RefreshReplicable(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ApplyDirtyGroups(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.ApplyDirtyGroups");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ApplyDirtyGroups(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_FilterStateSync(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.FilterStateSync");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_FilterStateSync(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_AddForClient(ref ProfilerTimer __local_timer, IMyReplicable replicable)
    {
        __local_timer = Profiler.Start("MyReplicationServer.AddForClient", profileMemory: true, new(replicable));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_AddForClient(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SendStreamingEntry(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.SendStreamingEntry");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SendStreamingEntry(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ScheduleStateGroupSync(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyReplicationServer.ScheduleStateGroupSync");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ScheduleStateGroupSync(ref ProfilerTimer __local_timer)
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
