using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ProtoBuf;

namespace VisualProfiler;

public static class Profiler
{
    [ThreadStatic]
    internal static ProfilerGroup? ThreadGroup;

    static Dictionary<int, ProfilerGroup> profilerGroupsById = [];
    static Dictionary<string, int> sortingGroups = [];

    public static ProfilerGroup[] GetProfilerGroups()
    {
        lock (profilerGroupsById)
            return profilerGroupsById.Values.ToArray();
    }

    public static void GetProfilerGroups(List<ProfilerGroup> groups)
    {
        lock (profilerGroupsById)
            groups.AddRange(profilerGroupsById.Values);
    }

    public static int TotalTimerCount
    {
        get
        {
            int count = 0;

            lock (profilerGroupsById)
            {
                foreach (var group in profilerGroupsById.Values)
                    count += group.TimerCount;
            }

            return count;
        }
    }

    static long frameIndex;

    public static bool IsEnabled => isEnabled;
    static bool isEnabled;

    public static bool IsRecordingEvents => isRecordingEvents;
    static bool isRecordingEvents;

    public static bool IsRecordingOutliers => isRecordingOutliers;
    static bool isRecordingOutliers;

    internal static IProfilerEventDataObjectResolver? EventObjectResolver;

    static int? numFramesToRecord;
    static Action<ProfilerEventsRecording>? recordingCompletedCallback;
    static DateTime recordingStartTime;

    static List<(long FrameIndex, List<(int GroupId, ProfilerEvent[] Events)> Groups)> recordedFrameEventsPerGroup = [];

    public static bool IsTimerActive()
    {
        var group = GetOrCreateGroupForCurrentThread();

        return group.ActiveTimer != null;
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    #region Start / Stop

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ProfilerEventHandle StartLite(ProfilerKey key, ProfilerTimerOptions options, in ProfilerEvent.ExtraData extraData)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = GetOrCreateGroupForCurrentThread();
        int depth = ++group.CurrentDepth;

        if (!isEnabled) // TODO: Should this check isRecordingEvents?
            return new ProfilerEventHandle { Depth = depth };

        bool profileMemory = (options & ProfilerTimerOptions.ProfileMemory) != 0;

        ref var _event = ref group.StartEvent(out var array, out int eventIndex);
        _event.NameKey = key.GlobalIndex;
        _event.Flags = profileMemory ? ProfilerEvent.EventFlags.MemoryTracked : ProfilerEvent.EventFlags.None;
        _event.Depth = depth;
        _event.ExtraValue = extraData;

        long memory = profileMemory ? GC.GetAllocatedBytesForCurrentThread() : 0;

        _event.MemoryBefore = _event.MemoryAfter = memory;

        var handle = new ProfilerEventHandle {
            Array = array,
            EventIndex = eventIndex,
            Depth = depth
        };

        _event.StartTime = _event.EndTime = Stopwatch.GetTimestamp();

        return handle;
    }

    public static ProfilerTimer Start(int index, string name, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(index, name, options, group.ActiveTimer, group.CurrentDepth);

        Assert.True(timer.Depth == group.CurrentDepth + 1);

        group.ActiveTimer = timer;
        group.CurrentDepth = timer.Depth;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(ProfilerKey key, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(key, options, group.ActiveTimer, group.CurrentDepth);

        Assert.True(timer.Depth == group.CurrentDepth + 1);

        group.ActiveTimer = timer;
        group.CurrentDepth = timer.Depth;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(string name, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, options, group.ActiveTimer, group.CurrentDepth);

        Assert.True(timer.Depth == group.CurrentDepth + 1);

        group.ActiveTimer = timer;
        group.CurrentDepth = timer.Depth;

        timer.StartInternal(extraData);

        return timer;
    }

    [MethodImpl(Inline)] public static ProfilerTimer Start(int index, string name) => Start(index, name, ProfilerTimerOptions.ProfileMemory, default);
    [MethodImpl(Inline)] public static ProfilerTimer Start(string name) => Start(name, ProfilerTimerOptions.ProfileMemory, default);
    [MethodImpl(Inline)] public static ProfilerTimer Start(ProfilerKey key, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory) => Start(key, options, default);
    [MethodImpl(Inline)] public static ProfilerTimer Start(string name, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory) => Start(name, options, default);

    public static ProfilerTimer Restart(int index, string name, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(index, name, options, timer.Parent, group.CurrentDepth - 1);

        Assert.True(timer.Depth == group.CurrentDepth);

        group.ActiveTimer = timer;

        timer.StartInternal(default);

        return timer;
    }

    public static ProfilerTimer Restart(ProfilerKey key, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(key, options, timer.Parent, group.CurrentDepth - 1);

        Assert.True(timer.Depth == group.CurrentDepth);

        group.ActiveTimer = timer;

        timer.StartInternal(default);

        return timer;
    }

    public static ProfilerTimer Restart(string name, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(name, options, timer.Parent, group.CurrentDepth - 1);

        Assert.True(timer.Depth == group.CurrentDepth);

        group.ActiveTimer = timer;

        timer.StartInternal(default);

        return timer;
    }

    [MethodImpl(Inline)] public static ProfilerTimer Restart(string name) => Restart(name, ProfilerTimerOptions.ProfileMemory);

    public static void Stop()
    {
        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        group.StopActiveTimer();
    }

    public static void AddTimeValue(string name, long elapsedTicks)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, ProfilerTimerOptions.None, group.ActiveTimer, group.CurrentDepth);

        timer.AddElapsedTicks(elapsedTicks);
    }

    public static ProfilerTimer OpenTimer(int groupId, string name)
    {
        Assert.NotNull(name, "Name must not be null");

        if (!profilerGroupsById.TryGetValue(groupId, out var group))
            throw new ArgumentException("Invalid groupId.", nameof(groupId));

        var timer = group.GetOrCreateTimer(name, ProfilerTimerOptions.None, group.ActiveTimer, group.CurrentDepth);

        return timer;
    }

    [Conditional("DEBUG")]
    public static void StartDebug(string name, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory) => Start(name, options);

    [Conditional("DEBUG")]
    public static void StopDebug() => Stop();

    public static void UnwindToDepth(int depth)
    {
        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        group.UnwindToDepth(depth);
    }

    #endregion

    public static void SetIsRealtimeThread(bool isRealtime)
    {
        var group = GetOrCreateGroupForCurrentThread();
        group.IsRealtimeThread = isRealtime;
    }

    #region Group sorting

    public static void SetSortingGroupForCurrentThread(string sortingGroup, int orderInGroup = 0)
    {
        var group = GetOrCreateGroupForCurrentThread();
        group.SortingGroup = sortingGroup;
        group.OrderInSortingGroup = orderInGroup;
    }

    public static void SetSortingGroupOrderPriority(string sortingGroup, int priority)
    {
        lock (sortingGroups)
            sortingGroups[sortingGroup] = priority;
    }

    public static int SetSortingGroupOrderPriority(string sortingGroup)
    {
        lock (sortingGroups)
            return sortingGroups[sortingGroup];
    }

    public static int CompareSortingGroups(string groupA, string groupB)
    {
        lock (sortingGroups)
        {
            if (!sortingGroups.TryGetValue(groupA, out int pa))
                return string.Compare(groupA, groupB);

            if (sortingGroups.TryGetValue(groupB, out int pb))
                return pb.CompareTo(pa);

            return -1;
        }
    }

    #endregion

    #region Frames

    public static void BeginFrameForCurrentThread()
    {
        ThreadGroup?.BeginFrame();
    }

    public static void BeginFrameForThreadID(int threadId)
    {
        lock (profilerGroupsById)
        {
            if (profilerGroupsById.TryGetValue(threadId, out var group))
                group.BeginFrame();
        }
    }

    public static void EndFrameForCurrentThread()
    {
        ThreadGroup?.EndFrame();
    }

    public static void EndFrameForThreadID(int threadId)
    {
        lock (profilerGroupsById)
        {
            if (profilerGroupsById.TryGetValue(threadId, out var group))
                group.EndFrame();
        }
    }

    internal static void AddOutlierFrameGroupEvents(ProfilerGroup group, ProfilerEvent[] events)
    {
        (long FrameIndex, List<(int GroupId, ProfilerEvent[] Events)> Groups) frame;

        if (recordedFrameEventsPerGroup.Count > 0)
            frame = recordedFrameEventsPerGroup[^1];
        else
            frame = default;

        if (frame.Groups == null || frame.FrameIndex != frameIndex)
        {
            const int maxRecordedFrames = 20;

            if (recordedFrameEventsPerGroup.Count >= maxRecordedFrames)
                recordedFrameEventsPerGroup.RemoveAt(0);

            recordedFrameEventsPerGroup.Add(frame = (frameIndex, []));
        }

        frame.Groups.Add((group.ID, events));
    }

    #endregion

    #region Thread Timers

    [MethodImpl(Inline)]
    public static ProfilerTimer? GetThreadTimer(int index)
    {
        var group = ThreadGroup;

        if (group == null)
            return null;

        ProfilerTimer? timer;

        if (group.ActiveTimer != null)
            timer = group.ActiveTimer.subTimers[index];
        else
            timer = group.rootTimers[index];

        return timer;
    }

    [MethodImpl(Inline)]
    public static ProfilerTimer? GetThreadTimer([CallerMemberName] string name = null!)
    {
        return ThreadGroup?.GetTimer(name);
    }

    public static ProfilerTimer GetOrCreateThreadTimer([CallerMemberName] string name = null!, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory)
    {
        Assert.NotNull(name, "Name must not be null");

        return GetOrCreateGroupForCurrentThread().GetOrCreateTimer(name, options);
    }

    #endregion

    #region Groups

    [MethodImpl(Inline)]
    public static ProfilerGroup? GetGroupForCurrentThread() => ThreadGroup;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ProfilerGroup CreateGroupForCurrentThread()
    {
        var thread = Thread.CurrentThread;

        ProfilerGroup? group;

        lock (profilerGroupsById)
        {
            // NOTE: This can replace old entries from threads that have stopped and had
            // their ID reused. Thread ID's are only unique among running threads not over
            // all threads that have run during an application cycle.

            if (profilerGroupsById.TryGetValue(thread.ManagedThreadId, out group) && group.IsWaitingForFirstUse)
                group.IsWaitingForFirstUse = false;
            else
                profilerGroupsById[thread.ManagedThreadId] = group = new ProfilerGroup(thread.Name!, thread);
        }

        ThreadGroup = group;

        return group;
    }

    internal static ProfilerGroup CreateGroupForThread(Thread thread)
    {
        var group = new ProfilerGroup(thread.Name!, thread);

        lock (profilerGroupsById)
            profilerGroupsById[group.ID] = group;

        return group;
    }

    [MethodImpl(Inline)]
    static ProfilerGroup GetOrCreateGroupForCurrentThread()
    {
        return ThreadGroup ?? CreateGroupForCurrentThread();
    }

    public static int CreateGroup(string name)
    {
        int groupId = 0;

        lock (profilerGroupsById)
        {
            // Use negative IDs for special groups
            foreach (var item in profilerGroupsById)
                groupId = Math.Min(groupId, item.Key);

            groupId--;

            profilerGroupsById[groupId] = new ProfilerGroup(name, groupId);
        }

        return groupId;
    }

    public static ProfilerGroup? GetProfilerGroupForThreadID(int threadId)
    {
        lock (profilerGroupsById)
            return profilerGroupsById.GetValueOrDefault(threadId);
    }

    public static void ClearTimersForCurrentThread()
    {
        ThreadGroup?.ClearTimers();
    }

    public static void ClearTimersForThreadId(int threadId)
    {
        lock (profilerGroupsById)
        {
            if (profilerGroupsById.TryGetValue(threadId, out var group))
                group.ClearTimers();
        }
    }

    public static void ClearSubTimersForCurrentThread()
    {
        ThreadGroup?.ClearSubTimers();
    }

    public static void RemoveGroupForCurrentThread()
    {
        RemoveGroupForThreadId(Environment.CurrentManagedThreadId);
        ThreadGroup = null;
    }

    public static void RemoveGroupForThreadId(int threadId)
    {
        lock (profilerGroupsById)
            profilerGroupsById.Remove(threadId);
    }

    #endregion

    #region Recording

    public static void SetEnabled(bool enabled)
    {
        if (isRecordingEvents) throw new InvalidOperationException("Cannot change profiler enabled state while event recording is in progress.");

        isEnabled = enabled;
    }

    public static void SetEventObjectResolver(IProfilerEventDataObjectResolver? objectResolver)
    {
        EventObjectResolver = objectResolver;
    }

    static void ClearObjectResolverCache()
    {
        var eventObjectResolver = EventObjectResolver;

        if (eventObjectResolver != null)
        {
            lock (eventObjectResolver)
                eventObjectResolver.ClearCache();
        }
    }

    public static void StartEventRecording(int? numFrames = null, Action<ProfilerEventsRecording>? completedCallback = null)
    {
        if (isRecordingEvents) throw new InvalidOperationException("Event recording has already started.");

        isEnabled = true;

        numFramesToRecord = numFrames;

        if (numFrames.HasValue)
            recordingCompletedCallback = completedCallback;

        isRecordingEvents = true;
        recordingStartTime = DateTime.UtcNow;

        lock (profilerGroupsById)
        {
            foreach (var item in profilerGroupsById.Values)
                item.StartEventRecording();
        }

        ClearObjectResolverCache();
    }

    public static void EndOfFrame()
    {
        frameIndex++;

        if (!isEnabled)
            return;

        if (isRecordingEvents)
        {
            if (numFramesToRecord.HasValue && ThreadGroup != null
                && ThreadGroup.NumRecordedFrames >= numFramesToRecord.Value)
            {
                var recording = StopEventRecording();

                numFramesToRecord = null;
                recordingCompletedCallback?.Invoke(recording);
                recordingCompletedCallback = null;
            }
        }
        else
        {
            ClearObjectResolverCache();
        }
    }

    public static ProfilerEventsRecording StopEventRecording()
    {
        if (!isRecordingEvents) throw new InvalidOperationException("Event recording has not yet been started.");

        lock (profilerGroupsById)
        {
            if (!isRecordingEvents)
                return null!;

            isRecordingEvents = false;

            var groups = new List<ProfilerGroup>(profilerGroupsById.Count);
            var groupsRecordings = new List<(int, ProfilerEventsRecordingGroup)>(profilerGroupsById.Count);

            foreach (var item in profilerGroupsById)
            {
                var events = item.Value.StopEventRecording();

                if (events != null)
                {
                    groups.Add(item.Value);
                    groupsRecordings.Add((item.Key, events));
                }
            }

            ClearObjectResolverCache();

            int numRecordedFrames = 0;

            for (int i = 0; i < groupsRecordings.Count; i++)
                numRecordedFrames = Math.Max(numRecordedFrames, groupsRecordings[i].Item2.GetNumRecordedFrames());

            var groupRecsArray = groupsRecordings.ToArray();

            Array.Sort(groups.ToArray(), groupRecsArray, GroupComparer.Instance);

            return new ProfilerEventsRecording(recordingStartTime, numRecordedFrames, groupRecsArray);
        }
    }

    class GroupComparer : IComparer<ProfilerGroup>
    {
        public static readonly GroupComparer Instance = new();

        public int Compare(ProfilerGroup? x, ProfilerGroup? y)
        {
            int order = 0;

            if (x!.SortingGroup != null)
            {
                if (y!.SortingGroup != null)
                    order = CompareSortingGroups(x.SortingGroup, y.SortingGroup);
                else
                    order = -1;
            }
            else if (y!.SortingGroup != null)
            {
                order = 1;
            }

            if (order == 0)
            {
                order = x.OrderInSortingGroup.CompareTo(y.OrderInSortingGroup);

                if (order == 0)
                    order = string.Compare(x.Name, y.Name);
            }

            return order;
        }
    }

    #endregion

    [MethodImpl(Inline)]
    public static void AddEvent(string name, ProfilerEvent.ExtraData extraData)
    {
        if (!isRecordingEvents)
            return;

        // Allows better inlining of AddEvent
        AddEventInternal(name).ExtraValue = extraData;
    }

    [MethodImpl(Inline)]
    public static void AddEvent(string name)
    {
        if (!isRecordingEvents)
            return;

        // Allows better inlining of AddEvent
        AddEventInternal(name).ExtraValue = default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref ProfilerEvent AddEventInternal(string name)
    {
        var nameKey = ProfilerKeyCache.GetOrAdd(name);
        var group = GetOrCreateGroupForCurrentThread();

        ref var _event = ref group.StartEvent(out _, out _);
        _event.NameKey = nameKey.GlobalIndex;
        _event.Flags = ProfilerEvent.EventFlags.SinglePoint;
        _event.StartTime = _event.EndTime = Stopwatch.GetTimestamp();
        _event.MemoryBefore = _event.MemoryAfter = 0;
        _event.Depth = group.CurrentDepth;

        return ref _event;
    }
}

[Flags]
public enum ProfilerTimerOptions
{
    None = 0,
    ProfileMemory = 1
}

public interface IProfilerEventDataObjectResolver
{
    void Resolve(ref ProfilerEvent.ExtraData data);

    void ResolveNonCached(ref ProfilerEvent.ExtraData data);

    void ClearCache();
}

public readonly struct ProfilerKey
{
    internal readonly int GlobalIndex;

    internal ProfilerKey(int globalIndex) => GlobalIndex = globalIndex;

    public override string ToString() => ProfilerKeyCache.GetName(this);
}

static class ProfilerKeyCache
{
    static readonly object lockObj = new();
    static readonly Dictionary<string, int> namesToKeys = [];
    static readonly Dictionary<int, string> keysToNames = [];
    static int keyGenerator = -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProfilerKey GetOrAdd(string name)
    {
        return GetOrAdd(name, out _);
    }

    public static ProfilerKey GetOrAdd(string name, out bool existing)
    {
        int key;

        lock (lockObj)
        {
            if (!(existing = namesToKeys.TryGetValue(name, out key)))
            {
                key = keyGenerator--;
                namesToKeys.Add(name, key);
                keysToNames.Add(key, name);
            }
        }

        return new ProfilerKey(key);
    }

    public static bool TryGet(string name, out ProfilerKey key)
    {
        int index;

        lock (lockObj)
        {
            if (!namesToKeys.TryGetValue(name, out index))
            {
                key = default;
                return false;
            }
        }

        key = new ProfilerKey(index);
        return true;
    }

    public static string GetName(ProfilerKey key)
    {
        lock (lockObj)
            return keysToNames[key.GlobalIndex];
    }

    public static Dictionary<int, string> GetStrings() => new(keysToNames);
}

public class ProfilerEventsAllocator
{
    public const int SegmentSize = 1024 * 4;

    public ProfilerEvent[][] Segments = [];
    public int NextIndex;

    public ref ProfilerEvent GetEvent(int index)
    {
        int segmentIndex = index / SegmentSize;

        return ref Segments[segmentIndex][index - segmentIndex * SegmentSize];
    }

    public ref ProfilerEvent Alloc(out ProfilerEvent[] array, out int index)
    {
        int i = NextIndex;
        int segmentIndex = i / SegmentSize;
        var segments = Segments;

        if (segmentIndex == segments.Length)
            segments = ExpandCapacity();

        NextIndex = i + 1;

        array = segments[segmentIndex];
        index = i - segmentIndex * SegmentSize;

        return ref array[index];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerEvent[][] ExpandCapacity()
    {
        // TODO: Add event for allocating new segment

        var segments = Segments;
        int newSegCount = segments.Length + 1;

        Array.Resize(ref segments, newSegCount);

        segments[^1] = new ProfilerEvent[SegmentSize];
        Segments = segments;

        return segments;
    }
}

[ProtoContract]
public class ProfilerEventsRecording
{
    // TODO: Version number

    [ProtoMember(1)] public string SessionName;
    [ProtoMember(2)] public DateTime StartTime;
    [ProtoMember(3)] public int NumFrames;
    [ProtoMember(4)] public Dictionary<int, ProfilerEventsRecordingGroup> Groups;
    [ProtoMember(5)] public Dictionary<int, string> EventStrings;
    [ProtoMember(6)] public Dictionary<int, string> DataStrings;
    [ProtoMember(7)] public Dictionary<int, RefObjWrapper> DataObjects;

    public TimeSpan ElapsedTime
    {
        get
        {
            long startTime = long.MaxValue;
            long endTime = 0;

            foreach (var item in Groups.Values)
            {
                if (item.EventSegments.Length == 0)
                    continue;

                var s = item.EventSegments[0];

                if (s.StartTime < startTime)
                    startTime = s.StartTime;

                s = item.EventSegments[^1];

                if (s.EndTime > endTime)
                    endTime = s.EndTime;
            }

            return TimeSpan.FromTicks(endTime - startTime);
        }
    }

    public ProfilerEventsRecording(DateTime startTime, int numFrames, (int GroupId, ProfilerEventsRecordingGroup Recording)[] groups)
    {
        SessionName = "";
        StartTime = startTime;
        NumFrames = numFrames;
        Groups = groups.ToDictionary(k => k.GroupId, e => e.Recording);
        EventStrings = ProfilerKeyCache.GetStrings();
        DataStrings = [];
        DataObjects = [];
    }

    public ProfilerEventsRecording()
    {
        SessionName = "";
        Groups = [];
        EventStrings = [];
        DataStrings = [];
        DataObjects = [];
    }

    public int[] GetOutlierFrames()
    {
        var frames = new HashSet<int>();

        foreach (var g in Groups)
        {
            foreach (var item in g.Value.OutlierFrames)
                frames.Add(item);
        }

        var array = frames.ToArray();

        Array.Sort(array);

        return array;
    }

    public (long StartTime, long EndTime) GetTimeBoundsForFrame(int frameIndex)
    {
        long startTime = long.MaxValue;
        long endTime = long.MinValue;

        foreach (var item in Groups)
        {
            var g = item.Value;

            if (frameIndex >= g.FrameStartEventIndices.Length
                || frameIndex >= g.FrameEndEventIndices.Length)
                continue;

            int startIndex = g.FrameStartEventIndices[frameIndex];
            int endIndex = g.FrameEndEventIndices[frameIndex];

            // Empty frame
            if (endIndex < startIndex)
                continue;

            long start = g.GetEvent(startIndex).StartTime;
            long end = g.GetEvent(endIndex).EndTime;

            Debug.Assert(end >= start);

            if (start < startTime)
                startTime = start;

            if (end > endTime)
                endTime = end;
        }

        if (startTime > endTime)
            (startTime, endTime) = (endTime, startTime);

        return (startTime, endTime);
    }
}

[ProtoContract]
public struct ProfilerEventsSegment
{
    [ProtoMember(1)] public long StartTime;
    [ProtoMember(2)] public long EndTime;
    [ProtoMember(3)] public int StartIndex;
    [ProtoMember(4)] public int Length;
}

[ProtoContract]
public class ProfilerEventsRecordingGroup
{
    [ProtoContract]
    public struct LegacySegment
    {
        [ProtoMember(1)] public ProfilerEvent[] Events;
        [ProtoMember(2)] public long StartTime;
        [ProtoMember(3)] public long EndTime;
    }

    [ProtoMember(1)] public string Name;

    [ProtoMember(2), Obsolete("Use Events Property instead.")]
    public LegacySegment[]? LegacySegments;

#pragma warning disable CS0618 // Type or member is obsolete
    [ProtoMember(7)]
    public ProfilerEvent[] Events
    {
        get
        {
            if (LegacySegments != null)
                LoadLegacyEvents(ref LegacySegments);

            return events;
        }
        set => events = value;
    }

    [ProtoMember(8)]
    public ProfilerEventsSegment[] EventSegments
    {
        get
        {
            if (LegacySegments != null)
                LoadLegacyEvents(ref LegacySegments);

            return eventSegments;
        }
        set => eventSegments = value;
    }
#pragma warning restore CS0618

    ProfilerEvent[] events;
    ProfilerEventsSegment[] eventSegments;

    [ProtoMember(3)] public int EventCount;
    [ProtoMember(4)] public int[] FrameStartEventIndices;
    [ProtoMember(5)] public int[] FrameEndEventIndices;
    [ProtoMember(6)] public int[] OutlierFrames;

    public ProfilerEventsRecordingGroup(string name, ProfilerEvent[][] events, int eventCount,
        int[] frameStartIndices, int[] frameEndIndices, int[] outlierFrames)
    {
        Name = name;
        FrameStartEventIndices = frameStartIndices;
        FrameEndEventIndices = frameEndIndices;
        OutlierFrames = outlierFrames;
        EventCount = eventCount;

        const int ss = ProfilerEventsAllocator.SegmentSize;
        int numSegments = (eventCount + ss - 1) / ss;

        this.events = new ProfilerEvent[eventCount];
        eventSegments = new ProfilerEventsSegment[numSegments];

        int ei = 0;

        for (int s = 0; s < eventSegments.Length; s++)
        {
            var segment = events[s];
            int endIndexInSegment = Math.Min(segment.Length - 1, (eventCount - 1) - s * ss);
            long endTime = segment[endIndexInSegment].EndTime;

            // The EndTime of the last event is not usually the end bounds of
            // the segment. The parent events end after the children but
            // come before them in the array.

            if (segment[endIndexInSegment].Depth != 0)
            {
                for (int i = endIndexInSegment - 1; i >= 0; i--)
                {
                    ref var e = ref segment[i];

                    if (e.EndTime > endTime)
                        endTime = e.EndTime;

                    if (e.Depth == 0)
                        break;
                }
            }

            eventSegments[s] = new() {
                StartTime = segment[0].StartTime,
                EndTime = endTime,
                StartIndex = ei,
                Length = endIndexInSegment + 1
            };

            for (int i = 0; i <= endIndexInSegment; i++)
                this.events[ei++] = segment[i];
        }
    }

    public ProfilerEventsRecordingGroup()
    {
        Name = "";
        events = [];
        eventSegments = [];
        FrameStartEventIndices = [];
        FrameEndEventIndices = [];
        OutlierFrames = [];
    }

    void LoadLegacyEvents([MaybeNull] ref LegacySegment[] legacySegments)
    {
        events = new ProfilerEvent[EventCount];
        eventSegments = new ProfilerEventsSegment[legacySegments.Length];

        int e = 0;

        for (int i = 0; i < legacySegments.Length; i++)
        {
            var segment = legacySegments[i];
            int segmentLen = Math.Min(segment.Events.Length, EventCount - e);

            eventSegments[i] = new() {
                StartTime = segment.StartTime,
                EndTime = segment.EndTime,
                StartIndex = e,
                Length = segmentLen
            };

            Array.Copy(segment.Events, 0, events, e, segmentLen);
            e += segment.Events.Length;
        }

        legacySegments = null;
    }

    public ref ProfilerEvent GetEvent(int index) => ref events[index];

    public int GetNumRecordedFrames()
    {
        int numStart = FrameStartEventIndices.Length;
        int numEnd = FrameEndEventIndices.Length;

        if (numStart == 0 || numEnd == 0)
            return 0;

        int firstStart = FrameStartEventIndices[0];
        int firstEnd = FrameEndEventIndices[0];

        if (firstEnd == -1 || events[firstEnd].EndTime < events[firstStart].StartTime)
            numEnd--; // Start of first frame is cut off

        int lastStart = FrameStartEventIndices[^1];
        int lastEnd = FrameEndEventIndices[^1];

        if (events[lastStart].StartTime > events[lastEnd].EndTime)
            numStart--; // End of last frame is cut off

        return Math.Min(numStart, numEnd);
    }
}
