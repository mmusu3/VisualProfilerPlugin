﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VisualProfiler;

public class ProfilerGroup
{
    internal ProfilerTimer? ActiveTimer;
    internal Dictionary<string, ProfilerKey> LocalKeyCache = [];
    internal int CurrentDepth = -1;

    public bool IsRealtimeThread = false;

    public IReadOnlyList<ProfilerTimer?> RootTimers => rootTimers;
    internal ProfilerTimer?[] rootTimers;

    public IReadOnlyList<ProfilerTimer> AllTimers => timers;
    internal readonly List<ProfilerTimer> timers;

    public int TimerCount => timers.Count;

    public readonly string Name;
    public readonly int ID;
    public readonly Thread? Thread;

    public string? SortingGroup;
    public int OrderInSortingGroup;

    internal bool IsWaitingForFirstUse;

    public ProfilerGroup(string name, Thread thread)
    {
        Name = name;
        ID = thread.ManagedThreadId;
        Thread = thread;
        timers = [];
        rootTimers = [];
    }

    public ProfilerGroup(string name, int id)
    {
        Name = name;
        ID = id;
        Thread = null;
        timers = [];
        rootTimers = [];
    }

    public override string ToString() => $"{Name}, Realtime: {IsRealtimeThread}, SortGroup: {SortingGroup}, SortPrio: {OrderInSortingGroup}";

    #region Get / Create Timers

    internal ProfilerTimer GetOrCreateRootTimer(string name, ProfilerTimerOptions options)
    {
        ProfilerKey key;
        ProfilerTimer? timer;

        if (LocalKeyCache.TryGetValue(name, out key))
        {
            timer = GetRootTimer(key);

            if (timer != null)
                return timer;
        }
        else
        {
            key = ProfilerKeyCache.GetOrAdd(name);
            LocalKeyCache.Add(name, key);
        }

        timer = CreateRootTimer(name, key, options);

        return timer;
    }

    public ProfilerTimer? GetTimer(string name)
    {
        ProfilerKey key;

        if (!LocalKeyCache.TryGetValue(name, out key))
            return null;

        if (ActiveTimer != null)
            return ActiveTimer.FindSubTimer(key);

        return GetRootTimer(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(string name, ProfilerTimerOptions options)
        => GetOrCreateTimer(name, options, ActiveTimer, CurrentDepth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ProfilerTimer GetOrCreateTimer(string name, ProfilerTimerOptions options, ProfilerTimer? parentTimer, int currentDepth)
    {
        ProfilerTimer? timer;
        bool existing;

        if (parentTimer != null)
        {
            timer = parentTimer.GetOrCreateSubTimer(name, options, currentDepth + 1, out existing);

            if (!existing)
                timers.Add(timer);
        }
        else
        {
            Assert.True(currentDepth == -1);

            timer = GetOrCreateRootTimer(name, options);
        }

        return timer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateRootTimer(string name, ProfilerKey key, ProfilerTimerOptions options)
    {
        var timer = new ProfilerTimer(name, key, options, this, null, 0);
        int index = rootTimers.Length;

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(int index, string name, ProfilerTimerOptions options)
        => GetOrCreateTimer(index, name, options, ActiveTimer, CurrentDepth);

    internal ProfilerTimer GetOrCreateTimer(int index, string name, ProfilerTimerOptions options, ProfilerTimer? parentTimer, int currentDepth)
    {
        ProfilerTimer? timer = null;

        if (parentTimer != null)
        {
            if ((uint)index < (uint)parentTimer.subTimers.Length)
                timer = parentTimer.subTimers[index];

            if (timer == null)
            {
                timer = parentTimer.CreateSubTimer(index, name, options, currentDepth + 1);
                timers.Add(timer);
            }
        }
        else
        {
            Assert.True(currentDepth == -1);

            if ((uint)index < (uint)rootTimers.Length)
                timer = rootTimers[index];

            timer ??= CreateRootTimer(index, name, options);
        }

        return timer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateRootTimer(int index, string name, ProfilerTimerOptions options)
    {
        ProfilerKey key;

        if (!LocalKeyCache.TryGetValue(name, out key))
        {
            key = ProfilerKeyCache.GetOrAdd(name);
            LocalKeyCache.Add(name, key);
        }

        var timer = new ProfilerTimer(name, key, options, group: this, parent: null, 0);

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(ProfilerKey key, ProfilerTimerOptions options)
        => GetOrCreateTimer(key, options, ActiveTimer, CurrentDepth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ProfilerTimer GetOrCreateTimer(ProfilerKey key, ProfilerTimerOptions options, ProfilerTimer? parentTimer, int currentDepth)
    {
        if (parentTimer != null)
        {
            return parentTimer.FindSubTimer(key) ?? CreateSubTimer(parentTimer, key, options, currentDepth);
        }
        else
        {
            Assert.True(currentDepth == -1);

            return GetRootTimer(key) ?? CreateRootTimer(key, options);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateSubTimer(ProfilerTimer parentTimer, ProfilerKey key, ProfilerTimerOptions options, int currentDepth)
    {
        var timer = parentTimer.CreateSubTimer(key, options, currentDepth + 1);
        timers.Add(timer);
        return timer;
    }

    public ProfilerTimer? GetRootTimer(ProfilerKey key)
    {
        for (int i = 0; i < rootTimers.Length; i++)
        {
            var t = rootTimers[i];

            if (t != null && t.Key.GlobalIndex == key.GlobalIndex)
                return t;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateRootTimer(ProfilerKey key, ProfilerTimerOptions options)
    {
        var name = ProfilerKeyCache.GetName(key);
        var timer = new ProfilerTimer(name, key, options, this, null, 0);
        int index = rootTimers.Length;

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    #endregion

    #region Start / Stop Timer

    public ProfilerTimer StartTimer(string name, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        var timer = GetOrCreateTimer(name, options, ActiveTimer, CurrentDepth++);
        ActiveTimer = timer;
        timer.StartInternal(extraData);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer StartTimer(string name, ProfilerTimerOptions options)
    {
        return StartTimer(name, options, default);
    }

    public ProfilerTimer RestartTimer(string name, ProfilerTimerOptions options)
    {
        var timer = ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();
        timer = GetOrCreateTimer(name, options, timer.Parent, CurrentDepth - 1);
        ActiveTimer = timer;
        timer.StartInternal(default);

        return timer;
    }

    public void StopActiveTimer()
    {
        var timer = ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        ActiveTimer = timer = timer.Parent;
        CurrentDepth = timer?.Depth ?? -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UnwindToDepth(int depth)
    {
        while (CurrentDepth > depth)
            StopActiveTimer();
    }

    #endregion

    public void ClearTimers()
    {
        if (ActiveTimer != null) throw new InvalidOperationException("All timers in this group must be stopped before clearing.");

        timers.Clear();
        rootTimers = [];
    }

    public void ClearSubTimers()
    {
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            if (timers[i].IsRunning)
                timers[i].ClearSubTimers();
            else
                timers.RemoveAt(i);
        }
    }

    object frameLock = new();

    ProfilerEventsAllocator currentEvents = new();

    int frameStartEventIndex = -1;
    int prevFrameEndEventIndex = -1;

    List<int> frameStartEventIndices = [];
    List<int> frameEndEventIndices = [];
    List<int> outlierFrameIndices = [];

    internal void AddEvent(int nameKey, long timestamp, ProfilerEvent.ExtraData extraData = default)
    {
        currentEvents.Alloc(out var array, out int index);

        array[index] = new ProfilerEvent {
            NameKey = nameKey,
            Flags = ProfilerEvent.EventFlags.SinglePoint,
            StartTime = timestamp,
            EndTime = timestamp,
            Depth = ActiveTimer?.Depth ?? 0,
            ExtraValue = extraData
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref ProfilerEvent StartEvent(out ProfilerEvent[] array, out int index)
    {
        return ref currentEvents.Alloc(out array, out index);
    }

    public void BeginFrame()
    {
        if (!Profiler.IsEnabled)
            return;

        lock (frameLock)
        {
            if (Profiler.IsRecordingEvents)
            {
                frameStartEventIndex = currentEvents.NextIndex;
                frameStartEventIndices.Add(frameStartEventIndex);
            }
        }
    }

    public void EndFrame()
    {
        if (!Profiler.IsEnabled)
            return;

        if (ActiveTimer != null) throw new InvalidOperationException($"Profiler group '{Name}' still has an active timer '{ActiveTimer.Name}'");

        bool hasOutliers = false;

        foreach (var item in rootTimers)
        {
            if (item == null)
                continue;

            item.EndFrame(out bool outliers);
            hasOutliers |= outliers;
        }

        lock (frameLock)
        {
            var events = currentEvents;
            int startIndex = prevFrameEndEventIndex + 1;
            int endIndex = events.NextIndex - 1;

            if (Profiler.IsRecordingEvents)
            {
                if (frameStartEventIndex != -1)
                    frameEndEventIndices.Add(endIndex);

                prevFrameEndEventIndex = endIndex;
            }

            frameStartEventIndex = -1;

            if (endIndex >= startIndex)
            {
                var eventObjectResolver = Profiler.EventObjectResolver;

                if (eventObjectResolver != null && (Profiler.IsRecordingEvents || (hasOutliers && Profiler.IsRecordingOutliers)))
                    ResolveObjects(events, startIndex, endIndex, eventObjectResolver);
            }

            if (Profiler.IsRecordingEvents)
            {
                if (hasOutliers && frameStartEventIndices.Count > 0)
                    outlierFrameIndices.Add(frameStartEventIndices.Count - 1);

                return;
            }

            if (hasOutliers && endIndex >= startIndex && Profiler.IsRecordingOutliers)
            {
                var eventsArray = new ProfilerEvent[endIndex - startIndex + 1];

                const int ss = ProfilerEventsAllocator.SegmentSize;

                int startSegmentIndex = startIndex / ss;
                int endSegmentIndex = endIndex / ss;
                int eIndex = 0;

                for (int i = startSegmentIndex; i <= endSegmentIndex; i++)
                {
                    var segment = events.Segments[i];
                    int startEventIndex = Math.Max(0, startIndex - i * ss);
                    int endEventIndex = Math.Min(segment.Length - 1, endIndex - i * ss);

                    for (int j = startEventIndex; j <= endEventIndex; j++)
                    {
                        eventsArray[eIndex++] = segment[j];
                        segment[j].ExtraValue.Object = null;
                    }
                }

                Profiler.AddOutlierFrameGroupEvents(this, eventsArray);
            }
            else
            {
                ClearEventsData();
            }

            Profiler.EventObjectResolver?.ClearCache();

            prevFrameEndEventIndex = -1;
            events.NextIndex = 0;
        }
    }

    void ClearEventsData()
    {
        const int ss = ProfilerEventsAllocator.SegmentSize;

        var events = currentEvents;

        int startIndex = prevFrameEndEventIndex + 1;
        int endIndex = events.NextIndex - 1;

        if (endIndex < startIndex)
            return;

        int startSegmentIndex = startIndex / ss;
        int endSegmentIndex = endIndex / ss;

        for (int i = startSegmentIndex; i <= endSegmentIndex; i++)
        {
            var segment = events.Segments[i];
            int startEventIndex = Math.Max(0, startIndex - i * ss);
            int endEventIndex = Math.Min(segment.Length - 1, endIndex - i * ss);

            for (int j = startEventIndex; j <= endEventIndex; j++)
                segment[j].ExtraValue.Object = null;
        }
    }

    internal void StartEventRecording()
    {
        lock (frameLock)
        {
            frameStartEventIndices.Clear();
            frameEndEventIndices.Clear();
            outlierFrameIndices.Clear();
        }
    }

    internal ProfilerEventsRecordingGroup? StopEventRecording()
    {
        lock (frameLock)
        {
            // NOTE: ProfilerTimers can still be ending their events concurrently.

            var recordedEvents = currentEvents;
            currentEvents = new ProfilerEventsAllocator();

            int startIndex = prevFrameEndEventIndex + 1;
            int endIndex = recordedEvents.NextIndex - 1;

            var eventObjectResolver = Profiler.EventObjectResolver;

            if (eventObjectResolver != null)
            {
                if (endIndex >= startIndex)
                    ResolveObjects(recordedEvents, startIndex, endIndex, eventObjectResolver);

                eventObjectResolver.ClearCache();
            }

            ProfilerEventsRecordingGroup? recording = null;

            if (recordedEvents.NextIndex > 0)
                recording = new ProfilerEventsRecordingGroup(Name, recordedEvents, frameStartEventIndices.ToArray(), frameEndEventIndices.ToArray(), outlierFrameIndices.ToArray());

            prevFrameEndEventIndex = -1;

            return recording;
        }
    }

    static void ResolveObjects(ProfilerEventsAllocator recordedEvents, int startIndex, int endIndex, IProfilerEventDataObjectResolver objectResolver)
    {
        const int ss = ProfilerEventsAllocator.SegmentSize;

        int startSegmentIndex = startIndex / ss;
        int endSegmentIndex = endIndex / ss;

        for (int i = startSegmentIndex; i <= endSegmentIndex; i++)
        {
            var segment = recordedEvents.Segments[i];
            int startEventIndex = Math.Max(0, startIndex - i * ss);
            int endEventIndex = Math.Min(segment.Length - 1, endIndex - i * ss);

            for (int j = startEventIndex; j <= endEventIndex; j++)
            {
                ref var _event = ref segment[j];

                if (_event.ExtraValue.Type is ProfilerEvent.ExtraValueTypeOption.Object or ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory)
                {
                    if (_event.ExtraValue.Object != null)
                        objectResolver.Resolve(ref _event.ExtraValue);
                }
            }
        }
    }

    internal int NumRecordedFrames => GetNumRecordedFrames(currentEvents);

    public int GetNumRecordedFrames(ProfilerEventsAllocator events)
    {
        int numStart = frameStartEventIndices.Count;
        int numEnd = frameEndEventIndices.Count;

        if (numStart == 0 || numEnd == 0)
            return 0;

        int firstStart = frameStartEventIndices[0];
        int firstEnd = frameEndEventIndices[0];

        if (events.GetEvent(firstEnd).EndTime < events.GetEvent(firstStart).StartTime)
            numEnd--; // Start of first frame is cut off

        int lastStart = frameStartEventIndices[^1];
        int lastEnd = frameEndEventIndices[^1];

        if (events.GetEvent(lastStart).StartTime > events.GetEvent(lastEnd).EndTime)
            numStart--; // End of last frame is cut off

        return Math.Min(numStart, numEnd);
    }
}
