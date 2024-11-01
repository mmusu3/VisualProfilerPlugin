using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyReplicationServer_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

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
        var source = typeof(MyReplicationServer).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyReplicationServer_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Destroy;
        internal static ProfilerKey UpdateBefore;
        internal static ProfilerKey SendUpdate;
        internal static ProfilerKey RefreshReplicable;
        internal static ProfilerKey ApplyDirtyGroups;
        internal static ProfilerKey FilterStateSync;
        internal static ProfilerKey AddForClient;
        internal static ProfilerKey SendStreamingEntry;
        internal static ProfilerKey ScheduleStateGroupSync;
        internal static ProfilerKey ReplicableReady;
        internal static ProfilerKey ReplicableRequest;

        internal static void Init()
        {
            Destroy = ProfilerKeyCache.GetOrAdd("MyReplicationServer.Destroy");
            UpdateBefore = ProfilerKeyCache.GetOrAdd("MyReplicationServer.UpdateBefore");
            SendUpdate = ProfilerKeyCache.GetOrAdd("MyReplicationServer.SendUpdate");
            RefreshReplicable = ProfilerKeyCache.GetOrAdd("MyReplicationServer.RefreshReplicable");
            ApplyDirtyGroups = ProfilerKeyCache.GetOrAdd("MyReplicationServer.ApplyDirtyGroups");
            FilterStateSync = ProfilerKeyCache.GetOrAdd("MyReplicationServer.FilterStateSync");
            AddForClient = ProfilerKeyCache.GetOrAdd("MyReplicationServer.AddForClient");
            SendStreamingEntry = ProfilerKeyCache.GetOrAdd("MyReplicationServer.SendStreamingEntry");
            ScheduleStateGroupSync = ProfilerKeyCache.GetOrAdd("MyReplicationServer.ScheduleStateGroupSync");
            ReplicableReady = ProfilerKeyCache.GetOrAdd("MyReplicationServer.ReplicableReady");
            ReplicableRequest = ProfilerKeyCache.GetOrAdd("MyReplicationServer.ReplicableRequest");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)]
    static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop();}

    [MethodImpl(Inline)] static bool Prefix_Destroy(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Destroy); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateBefore(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.UpdateBefore, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.Network));
        return true;
    }

    [MethodImpl(Inline)] static bool Prefix_SendUpdate(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SendUpdate); return true; }

    [MethodImpl(Inline)] static bool Prefix_RefreshReplicable(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.RefreshReplicable); return true; }

    [MethodImpl(Inline)] static bool Prefix_ApplyDirtyGroups(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ApplyDirtyGroups); return true; }

    [MethodImpl(Inline)] static bool Prefix_FilterStateSync(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.FilterStateSync); return true; }

    [MethodImpl(Inline)] static bool Prefix_AddForClient(ref ProfilerTimer __local_timer, IMyReplicable replicable)
    { __local_timer = Profiler.Start(Keys.AddForClient, ProfilerTimerOptions.ProfileMemory, new(replicable)); return true; }

    [MethodImpl(Inline)] static bool Prefix_SendStreamingEntry(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SendStreamingEntry); return true; }

    [MethodImpl(Inline)] static bool Prefix_ScheduleStateGroupSync(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ScheduleStateGroupSync); return true; }

    [MethodImpl(Inline)] static bool Prefix_ReplicableReady(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ReplicableReady); return true; }

    [MethodImpl(Inline)] static bool Prefix_ReplicableRequest(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.ReplicableRequest); return true; }
}
