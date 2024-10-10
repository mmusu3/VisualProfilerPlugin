﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using ProtoBuf;
using VRageMath;

namespace VisualProfiler;

[ProtoContract]
public struct ProfilerEvent
{
    [Flags]
    public enum EventFlags : byte
    {
        None = 0,
        MemoryTracked = 1,
        SinglePoint = 2
    }

    public enum ExtraValueTypeOption
    {
        None = 0,
        Object = 1,
        Long = 2,
        Double = 3,
        Float = 4
    }

    [ProtoContract]
    public struct ExtraValueUnion
    {
        [ProtoMember(1)]
        public long DataField;

        [ProtoIgnore]
        public long LongValue
        {
            readonly get => DataField;
            set => DataField = value;
        }

        [ProtoIgnore]
        public double DoubleValue
        {
            get => Unsafe.As<long, double>(ref DataField);
            set => Unsafe.As<long, double>(ref DataField) = value;
        }

        [ProtoIgnore]
        public float FloatValue
        {
            get => Unsafe.As<long, float>(ref DataField);
            set
            {
                DataField = 0;
                Unsafe.As<long, float>(ref DataField) = value;
            }
        }

        public ExtraValueUnion(long value)
        {
            DataField = 0;
            LongValue = value;
        }

        public ExtraValueUnion(double value)
        {
            DataField = 0;
            DoubleValue = value;
        }

        public ExtraValueUnion(float value)
        {
            DataField = 0;
            FloatValue = value;
        }
    }

    [ProtoContract]
    public struct ExtraData
    {
        [ProtoMember(1)] public ExtraValueTypeOption Type;
        [ProtoMember(2)] public ExtraValueUnion Value;

        [ProtoIgnore] public object? Object;

        [ProtoMember(3)]
        public ObjectId ObjectKey
        {
            readonly get => new ObjectId((int)Value.LongValue);
            set => Value.LongValue = value.ID;
        }

        [ProtoMember(4)] public string? Format;

        public ExtraData(object? obj, string? format = null)
        {
            Type = ExtraValueTypeOption.Object;
            Value = default;
            Object = obj;
            Format = format;
        }

        public ExtraData(long value, string? format = null)
        {
            Type = ExtraValueTypeOption.Long;
            Value = new(value);
            Format = format;
        }

        public ExtraData(double value, string? format = null)
        {
            Type = ExtraValueTypeOption.Double;
            Value = new(value);
            Format = format;
        }

        public ExtraData(float value, string? format = null)
        {
            Type = ExtraValueTypeOption.Float;
            Value = new(value);
            Format = format;
        }
    }

    public readonly string Name => ProfilerKeyCache.GetName(new(NameKey));

    [ProtoMember(1)] public int NameKey;
    [ProtoMember(2)] public EventFlags Flags;
    [ProtoMember(3)] public long StartTime;
    [ProtoMember(4)] public long EndTime;
    [ProtoMember(5)] public long MemoryBefore;
    [ProtoMember(6)] public long MemoryAfter;
    [ProtoMember(7)] public int Depth;
    [ProtoMember(8)] public ExtraData ExtraValue; // TOOD: Perhaps allocate from separate array

    // TODO: Event chains for async task tracking
    // public int Next;

    public readonly bool MemoryTracked => (Flags & EventFlags.MemoryTracked) != 0;
    public readonly bool IsSinglePoint => (Flags & EventFlags.SinglePoint) != 0;

    public readonly TimeSpan ElapsedTime => ProfilerTimer.TimeSpanFromTimestampTicks(EndTime - StartTime);
    public readonly double ElapsedMilliseconds => ProfilerTimer.MillisecondsFromTicks(EndTime - StartTime);
}

[ProtoContract]
public struct ProfilerEventsSegment
{
    [ProtoMember(1)] public ProfilerEvent[] Events;
    [ProtoMember(2)] public long StartTime;
    [ProtoMember(3)] public long EndTime;
}

public struct ProfilerEventEnumerator
{
    ProfilerEventsSegment[] segments;
    int totalCount;
    int currentIndex;

    public ProfilerEventEnumerator(ProfilerEventsSegment[] segments, int totalCount)
    {
        this.segments = segments;
        this.totalCount = totalCount;
        currentIndex = -1;
    }

    public readonly ProfilerEventEnumerator GetEnumerator() => this;

    const int SegmentSize = ProfilerGroup.EventsAllocator.SegmentSize;

    public readonly ref ProfilerEvent Current => ref segments[currentIndex / SegmentSize].Events[currentIndex % SegmentSize];

    public bool MoveNext()
    {
        int c = currentIndex + 1;

        if (c >= totalCount)
            return false;

        currentIndex = c;
        return true;
    }
}

[ProtoContract]
public class GCEventInfo
{
    [ProtoMember(1)] public int Gen0Collections;
    [ProtoMember(2)] public int Gen1Collections;
    [ProtoMember(3)] public int Gen2Collections;

    public GCEventInfo(Vector3I collections)
    {
        Gen0Collections = collections.X;
        Gen1Collections = collections.Y;
        Gen2Collections = collections.Z;
    }

    public GCEventInfo() { }

    public override string ToString()
    {
        return string.Join("\n", new string[] { "Collections",
                $"{(Gen0Collections > 0 ? $"Gen0: {Gen0Collections}" : "")}",
                $"{(Gen1Collections > 0 ? $"Gen1: {Gen1Collections}" : "")}",
                $"{(Gen2Collections > 0 ? $"Gen2: {Gen2Collections}" : "")}" }.Where(s => s != ""));
    }
}

public readonly struct ProfilerKey
{
    internal readonly int GlobalIndex;

    internal ProfilerKey(int globalIndex) => GlobalIndex = globalIndex;

    public override string ToString() => ProfilerKeyCache.GetName(this);
}

public sealed class ProfilerTimer : IDisposable
{
    public readonly string Name;
    public readonly ProfilerKey Key;

    long startTimestamp;
    long elapsedTicks;

    public bool IsRunning => isRunning;
    bool isRunning;
    bool wasRun;

    ProfilerEvent[]? eventArray;
    int eventIndex = -1;

    readonly ProfilerGroup group;
    public readonly ProfilerTimer? Parent;
    public readonly bool ProfileMemory;

    public readonly int Depth;

    public IReadOnlyList<ProfilerTimer?> SubTimers => subTimers;
    internal ProfilerTimer?[] subTimers;

    internal readonly Dictionary<int, int> subTimersMap;

    public const int BufferSize = 300;

    public int CurrentIndex;

    public long MemoryBefore;
    public long MemoryAfter;

    public long InclusiveMemoryTotal;
    public long ExclusiveMemoryTotal;

    public long[] InclusiveMemoryDeltas;
    public long InclusiveMemoryDelta;

    public long[] ExclusiveMemoryDeltas;
    public long ExclusiveMemoryDelta;

    public struct GCCountsInfo
    {
        public Vector3I[] History;

        public Vector3I Before;
        public Vector3I After;
        public Vector3I Inclusive;
        public Vector3I Exclusive;
        public Vector3I CumulativeExclusive;

        internal Vector3I CumulativeInclusiveForChildren;
        internal long FirstChildStartTime;
        internal Vector3I FirstChildBefore;
    }

    public GCCountsInfo GCCounts;

#if NET7_0_OR_GREATER
    public TimeSpan GCTimeBefore;
    public TimeSpan GCTimeAfter;
    public TimeSpan GCTimeDelta;
    public TimeSpan[] GCTimes;
#endif

    public long[] InclusiveTimes;
    public long[] ExclusiveTimes;

    public long TimeInclusive;
    public long TimeExclusive;

    public double AverageExclusiveTime;
    public double ExclusiveTimeVariance;

    public int[] InvokeCounts;
    int prevInvokeCount;

    const int outlierAveragingSampleRange = 50;
    const int minTicksForOutlier = 1000;
    const double maxOutlierDeviationFraction = 5;

    const long TicksPerMicrosecond = 10;
    const long TicksPerMillisecond = 10000;
    const long TicksPerSecond = TicksPerMillisecond * 1000;
    static double tickFrequency = (double)TicksPerSecond / Stopwatch.Frequency;

    public TimeSpan ElapsedTime => TimeSpanFromTimestampTicks(elapsedTicks);

    static ProfilerKey GCKey = ProfilerKeyCache.GetOrAdd("GC");

    public static double MillisecondsFromTicks(long ticks) => TimeSpanFromTimestampTicks(ticks).TotalMilliseconds;

    public static double MicrosecondsFromTicks(long ticks)
    {
        long dateTimeTicks = Stopwatch.IsHighResolution
            ? unchecked((long)((double)ticks * tickFrequency))
            : ticks;

        return dateTimeTicks / (double)TicksPerMicrosecond;
    }

    public static TimeSpan TimeSpanFromTimestampTicks(long ticks)
    {
        long dateTimeTicks = Stopwatch.IsHighResolution
            ? unchecked((long)((double)ticks * tickFrequency))
            : ticks;

        return new TimeSpan(dateTimeTicks);
    }

    internal ProfilerTimer(string name, ProfilerKey key, bool profileMemory, ProfilerGroup group, ProfilerTimer? parent)
    {
        Name = name;
        Key = key;
        ProfileMemory = profileMemory;
        this.group = group;

        Assert.False(parent == this);

        Parent = parent;

        if (parent != null)
            Depth = parent.Depth + 1;

        GCCounts.History = new Vector3I[BufferSize];
#if NET7_0_OR_GREATER
        GCTimes = new TimeSpan[BufferSize];
#endif
        InclusiveTimes = new long[BufferSize];
        ExclusiveTimes = new long[BufferSize];
        InclusiveMemoryDeltas = new long[BufferSize];
        ExclusiveMemoryDeltas = new long[BufferSize];
        InvokeCounts = new int[BufferSize];
        subTimers = [];
        subTimersMap = [];
    }

    public ProfilerTimer? FindSubTimer(ProfilerKey key)
    {
        if (subTimersMap.TryGetValue(key.GlobalIndex, out int index))
            return subTimers[index];

        return null;
    }

    public ProfilerTimer? FindSubTimer(string name)
    {
        if (!ProfilerKeyCache.TryGet(name, out var key, group))
            return null;

        if (subTimersMap.TryGetValue(key.GlobalIndex, out int index))
            return subTimers[index];

        return null;
    }

    public void Start()
    {
        Assert.NotNull(group);
        Assert.False(group.ActiveTimer == this);

        group.ActiveTimer = this;

        StartInternal(default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StartInternal()
    {
        StartInternal(default);
    }

    internal void StartInternal(ProfilerEvent.ExtraData extraData)
    {
        if (isRunning)
        {
            ThrowTimerAlreadyRunning();

            [DoesNotReturn]
            //[StackTraceHidden]
            static void ThrowTimerAlreadyRunning() => throw new InvalidOperationException("Timer is already running");
        }

        isRunning = true;

        prevInvokeCount++;

        ref var _event = ref Unsafe.NullRef<ProfilerEvent>();

        if (group.IsRealtimeThread || Profiler.IsRecordingEvents)
        {
            group.StartEvent(out eventArray, out eventIndex);

            _event = ref eventArray[eventIndex];
            _event.NameKey = Key.GlobalIndex;
            _event.Depth = Depth;
            _event.ExtraValue = extraData;

            if (extraData.Type == ProfilerEvent.ExtraValueTypeOption.Object && !group.IsRealtimeThread)
                Profiler.EventObjectResolver?.ResolveNonCached(ref _event.ExtraValue);
        }

        if (ProfileMemory)
        {
            MemoryBefore = GC.GetAllocatedBytesForCurrentThread();

            if (Profiler.IsRecordingEvents)
            {
                GCCounts.Before.X = GC.CollectionCount(0);
                GCCounts.Before.Y = GC.CollectionCount(1);
                GCCounts.Before.Z = GC.CollectionCount(2);
                GCCounts.CumulativeInclusiveForChildren = default;
            }

#if NET7_0_OR_GREATER
            GCTimeBefore = GC.GetTotalPauseDuration();
#endif
        }

        if (eventIndex != -1)
        {
            _event.Flags = ProfileMemory ? ProfilerEvent.EventFlags.MemoryTracked : 0;
            _event.MemoryBefore = _event.MemoryAfter = MemoryBefore;
        }

        startTimestamp = Stopwatch.GetTimestamp();

        if (eventIndex != -1)
            _event.StartTime = _event.EndTime = startTimestamp;
    }

    public void StartOrSplit()
    {
        if (!isRunning)
        {
            Start();
            return;
        }

        prevInvokeCount++;

        if (!group.IsRealtimeThread && !Profiler.IsRecordingEvents)
            return;

        ref var _event = ref Unsafe.NullRef<ProfilerEvent>();

        if (eventArray != null && eventIndex != -1)
        {
            long endTimestamp = Stopwatch.GetTimestamp();

            _event = ref eventArray[eventIndex];
            _event.EndTime = endTimestamp;
            _event.MemoryAfter = ProfileMemory ? GC.GetAllocatedBytesForCurrentThread() : 0;

            EndGCCountsInfo(_event.EndTime);
        }

        group.StartEvent(out eventArray, out eventIndex);

        _event = ref eventArray[eventIndex];
        _event.NameKey = Key.GlobalIndex;
        _event.Depth = Depth;

        if (ProfileMemory)
        {
            _event.Flags = ProfilerEvent.EventFlags.MemoryTracked;
            _event.MemoryBefore = _event.MemoryAfter = GC.GetAllocatedBytesForCurrentThread();

            GCCounts.Before.X = GC.CollectionCount(0);
            GCCounts.Before.Y = GC.CollectionCount(1);
            GCCounts.Before.Z = GC.CollectionCount(2);
            GCCounts.CumulativeInclusiveForChildren = default;
        }
        else
        {
            _event.Flags = 0;
            _event.MemoryBefore = _event.MemoryAfter = 0;
        }

        _event.StartTime = _event.EndTime = Stopwatch.GetTimestamp();
        _event.ExtraValue = default;
    }

    void UnwindToDepth()
    {
        while (group.ActiveTimer != null && group.ActiveTimer.Depth > Depth)
            group.StopTimer();
    }

    public void Stop()
    {
        StopInternal();

        Assert.NotNull(group);
        Assert.True(group.ActiveTimer == this);

        group.ActiveTimer = Parent;
    }

    internal void StopInternal()
    {
        if (!isRunning)
        {
            ThrowTimerNotRunning();

            [DoesNotReturn]
            //[StackTraceHidden]
            static void ThrowTimerNotRunning() => throw new InvalidOperationException("Timer is not running. Must call Start first");
        }

        long endTimestamp = Stopwatch.GetTimestamp();
        elapsedTicks += endTimestamp - startTimestamp;

        isRunning = false;

        if (elapsedTicks < 0)
            elapsedTicks = 0;

        if (ProfileMemory)
        {
            MemoryAfter = GC.GetAllocatedBytesForCurrentThread();

            long memDelta = MemoryAfter - MemoryBefore;
            InclusiveMemoryDelta += memDelta;

            if (Profiler.IsRecordingEvents)
                EndGCCountsInfo(endTimestamp);

#if NET7_0_OR_GREATER
            GCTimeAfter = GC.GetTotalPauseDuration();
            GCTimeDelta = GCTimeAfter - GCTimeBefore;
#endif
        }

        if (eventArray != null && eventIndex != -1)
        {
            ref var _event = ref eventArray[eventIndex];
            _event.EndTime = endTimestamp;
            _event.MemoryAfter = MemoryAfter;

            eventArray = null;
            eventIndex = -1;
        }

        wasRun = true;
    }

    void EndGCCountsInfo(long endTimestamp)
    {
        ref var c = ref GCCounts;
        c.After.X = GC.CollectionCount(0);
        c.After.Y = GC.CollectionCount(1);
        c.After.Z = GC.CollectionCount(2);

        c.Inclusive = c.After - c.Before;
        c.Exclusive = c.Inclusive - c.CumulativeInclusiveForChildren;
        c.CumulativeExclusive += c.Exclusive;

        var exclusiveAfterChildren = c.Exclusive;

        if (c.FirstChildStartTime != 0)
        {
            var exclusiveBeforeChildren = c.FirstChildBefore - c.Before;
            exclusiveAfterChildren -= exclusiveBeforeChildren;

            if (exclusiveBeforeChildren != default)
                group.AddEvent(GCKey.GlobalIndex, c.FirstChildStartTime, new(new GCEventInfo(exclusiveBeforeChildren), "{0}"));

            c.FirstChildStartTime = 0;
        }

        if (exclusiveAfterChildren != default)
            group.AddEvent(GCKey.GlobalIndex, endTimestamp, new(new GCEventInfo(exclusiveAfterChildren), "{0}"));

        if (Parent != null)
        {
            ref var pc = ref Parent.GCCounts;

            // Children may be run more than once, sum their inclusive counts for
            // calculating parent exclusive count.
            pc.CumulativeInclusiveForChildren += c.Inclusive;

            if (pc.FirstChildStartTime == 0)
            {
                pc.FirstChildStartTime = startTimestamp;
                pc.FirstChildBefore = c.Before;
            }
        }
    }

    public void AddElapsedTicks(long elapsedTicks)
    {
        Assert.True(elapsedTicks >= 0);

        this.elapsedTicks += elapsedTicks;
        prevInvokeCount++;
        wasRun = true;
    }

    public void EndFrame(out bool hasOutliers)
    {
        hasOutliers = false;

        TimeInclusive = elapsedTicks;
        TimeExclusive = elapsedTicks;

        ExclusiveMemoryDelta = InclusiveMemoryDelta;

        bool subTimerWasRun = false;
        long subTimerTicks = 0;

        // Calculate exclusive time value
        foreach (var timer in subTimers)
        {
            if (timer == null)
                continue;

            if (timer.wasRun)
            {
                subTimerWasRun = true;
                subTimerTicks += timer.elapsedTicks;
                ExclusiveMemoryDelta -= timer.InclusiveMemoryDelta;
            }

            timer.EndFrame(out bool outliers);

            hasOutliers |= outliers;
        }

        Assert.True(wasRun || !subTimerWasRun);
        //Assert.True(elapsedTicks >= subTimerTicks);

        //TimeExclusive -= subTimerTicks;
        TimeExclusive = Math.Max(0, TimeExclusive - subTimerTicks);

        if (wasRun)
        {
            double deviation = TimeExclusive - AverageExclusiveTime;
            double d2 = deviation * deviation;

            if (ExclusiveTimeVariance > 0 && TimeExclusive > minTicksForOutlier)
            {
                double stdDev = Math.Sqrt(ExclusiveTimeVariance);
                double devFrac = deviation / stdDev;

                if (devFrac > maxOutlierDeviationFraction)
                    hasOutliers = true;
            }

            AverageExclusiveTime = AverageExclusiveTime * ((outlierAveragingSampleRange - 1) / (double)outlierAveragingSampleRange) + TimeExclusive * (1.0 / outlierAveragingSampleRange);
            ExclusiveTimeVariance = ExclusiveTimeVariance * ((outlierAveragingSampleRange - 1) / (double)outlierAveragingSampleRange) + d2 * (1.0 / outlierAveragingSampleRange);
        }

        InclusiveTimes[CurrentIndex] = TimeInclusive;
        ExclusiveTimes[CurrentIndex] = TimeExclusive;

        InclusiveMemoryDeltas[CurrentIndex] = InclusiveMemoryDelta;
        ExclusiveMemoryDeltas[CurrentIndex] = ExclusiveMemoryDelta;

        if (InclusiveMemoryDelta > 0)
            InclusiveMemoryTotal += InclusiveMemoryDelta;

        GCCounts.History[CurrentIndex] = GCCounts.CumulativeExclusive;
#if NET7_0_OR_GREATER
        GCTimes[CurrentIndex] = GCTimeDelta;
#endif

        InvokeCounts[CurrentIndex] = prevInvokeCount;

        CurrentIndex++;

        if (CurrentIndex == BufferSize)
            CurrentIndex = 0;

        Reset();
    }

    public void Reset()
    {
        Assert.False(isRunning);

        elapsedTicks = 0;
        isRunning = false;
        wasRun = false;
        startTimestamp = 0;
        prevInvokeCount = 0;
        InclusiveMemoryDelta = 0;

        GCCounts.CumulativeExclusive = default;
        GCCounts.CumulativeInclusiveForChildren = default;
        GCCounts.FirstChildStartTime = 0;
#if NET7_0_OR_GREATER
        GCTimeDelta = default;
#endif
    }

    public void CalculateAverageTimes(out double averageInclusiveTime, out double averageExclusiveTime)
    {
        const int averageRange = 100;
        averageInclusiveTime = 0;
        averageExclusiveTime = 0;

        for (int i = 1; i < averageRange + 1; i++)
        {
            long inclusive = InclusiveTimes[(BufferSize - i + CurrentIndex) % BufferSize];
            averageInclusiveTime += MillisecondsFromTicks(inclusive);

            long exclusive = ExclusiveTimes[(BufferSize - i + CurrentIndex) % BufferSize];
            averageExclusiveTime += MillisecondsFromTicks(exclusive);
        }

        averageInclusiveTime /= averageRange;
        averageExclusiveTime /= averageRange;
    }

    #region Get / Create Timers

    internal ProfilerTimer CreateSubTimer(int index, string name, bool profileMemory)
    {
        var key = ProfilerKeyCache.GetOrAdd(name, group);
        var timer = new ProfilerTimer(name, key, profileMemory, group, this);

        if (index >= subTimers.Length)
            Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(key.GlobalIndex, index);

        return timer;
    }

    internal ProfilerTimer CreateSubTimer(ProfilerKey key, bool profileMemory)
    {
        var name = ProfilerKeyCache.GetName(key);

        return CreateSubTimer(name, key, profileMemory);
    }

    internal ProfilerTimer CreateSubTimer(string name, ProfilerKey key, bool profileMemory)
    {
        var timer = new ProfilerTimer(name, key, profileMemory, group, this);

        int index;

        if (!subTimersMap.TryGetValue(key.GlobalIndex, out index))
            index = subTimers.Length;

        if (index >= subTimers.Length)
            Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(key.GlobalIndex, index);

        return timer;
    }

    public ProfilerTimer GetOrCreateSubTimer(int index, string name, bool profileMemory)
    {
        var timer = subTimers[index];

        if (timer != null)
            return timer;

        return CreateSubTimer(index, name, profileMemory);
    }

    public ProfilerTimer GetOrCreateSubTimer(string name, bool profileMemory)
    {
        var key = ProfilerKeyCache.GetOrAdd(name, group, out bool existing);
        var timer = existing ? FindSubTimer(name) : null;

        timer ??= CreateSubTimer(name, key, profileMemory);

        return timer;
    }

    #endregion

    internal void ClearSubTimers()
    {
        for (int i = subTimers.Length - 1; i >= 0; i--)
        {
            ref var timer = ref subTimers[i];

            if (timer == null)
                continue;

            if (timer.IsRunning)
                timer.ClearSubTimers();
            else
                timer = null;
        }
    }

    public void Dispose()
    {
        UnwindToDepth(); // In case of exceptions
        Stop();
    }

    public override string ToString() => $"Timer: {Name}, Profile Memory: {ProfileMemory}";
}

public class ProfilerGroup
{
    public IReadOnlyList<ProfilerTimer> AllTimers => timers;
    internal readonly List<ProfilerTimer> timers;

    public IReadOnlyList<ProfilerTimer?> RootTimers => rootTimers;
    internal ProfilerTimer?[] rootTimers;

    public int TimerCount => timers.Count;

    internal ProfilerTimer? ActiveTimer;

    public readonly string Name;
    public readonly int ID;
    public readonly Thread? Thread;

    public string? SortingGroup;
    public int OrderInSortingGroup;
    public bool IsRealtimeThread = false;

    internal Dictionary<string, int> LocalKeyCache = [];

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer? GetTimer(string name)
    {
        if (!ProfilerKeyCache.TryGet(name, out var key, this))
            return null;

        if (ActiveTimer != null)
            return ActiveTimer.FindSubTimer(key);

        return GetRootTimer(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(string name, bool profileMemory)
        => GetOrCreateTimer(name, profileMemory, ActiveTimer);

    internal ProfilerTimer GetOrCreateTimer(string name, bool profileMemory, ProfilerTimer? parentTimer)
    {
        var key = ProfilerKeyCache.GetOrAdd(name, this, out bool existing);

        if (parentTimer != null)
        {
            var timer = existing ? parentTimer.FindSubTimer(key) : null;

            if (timer == null)
            {
                timer = parentTimer.CreateSubTimer(name, key, profileMemory);
                timers.Add(timer);
            }

            return timer;
        }
        else
        {
            return GetRootTimer(key) ?? CreateRootTimer(name, key, profileMemory);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateRootTimer(string name, ProfilerKey key, bool profileMemory)
    {
        var timer = new ProfilerTimer(name, key, profileMemory, this, null);
        int index = rootTimers.Length;

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(int index, string name, bool profileMemory)
        => GetOrCreateTimer(index, name, profileMemory, ActiveTimer);

    internal ProfilerTimer GetOrCreateTimer(int index, string name, bool profileMemory, ProfilerTimer? parentTimer)
    {
        ProfilerTimer? timer = null;

        if (parentTimer != null)
        {
            if (index < parentTimer.subTimers.Length)
                timer = parentTimer.subTimers[index];

            if (timer == null)
            {
                timer = parentTimer.CreateSubTimer(index, name, profileMemory);
                timers.Add(timer);
            }
        }
        else
        {
            if (index < rootTimers.Length)
                timer = rootTimers[index];

            timer ??= CreateRootTimer(index, name, profileMemory);
        }

        return timer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateRootTimer(int index, string name, bool profileMemory)
    {
        var key = ProfilerKeyCache.GetOrAdd(name, this);
        var timer = new ProfilerTimer(name, key, profileMemory, group: this, parent: null);

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(ProfilerKey key, bool profileMemory)
        => GetOrCreateTimer(key, profileMemory, ActiveTimer);

    internal ProfilerTimer GetOrCreateTimer(ProfilerKey key, bool profileMemory, ProfilerTimer? parentTimer)
    {
        if (parentTimer != null)
            return parentTimer.FindSubTimer(key) ?? CreateSubTimer(parentTimer, key, profileMemory);
        else
            return GetRootTimer(key) ?? CreateRootTimer(key, profileMemory);
    }

    ProfilerTimer CreateSubTimer(ProfilerTimer parentTimer, ProfilerKey key, bool profileMemory)
    {
        var timer = parentTimer.CreateSubTimer(key, profileMemory);
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
    ProfilerTimer CreateRootTimer(ProfilerKey key, bool profileMemory)
    {
        var name = ProfilerKeyCache.GetName(key);
        var timer = new ProfilerTimer(name, key, profileMemory, this, null);
        int index = rootTimers.Length;

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    #endregion

    #region Start / Stop Timer

    public ProfilerTimer StartTimer(string name, bool profileMemory, ProfilerEvent.ExtraData extraData)
    {
        var timer = GetOrCreateTimer(name, profileMemory);
        ActiveTimer = timer;
        timer.StartInternal(extraData);

        return timer;
    }

    public ProfilerTimer StartTimer(string name, bool profileMemory)
    {
        var timer = GetOrCreateTimer(name, profileMemory, ActiveTimer);
        ActiveTimer = timer;
        timer.StartInternal();

        return timer;
    }

    public ProfilerTimer RestartTimer(string name, bool profileMemory)
    {
        var timer = ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();
        timer = GetOrCreateTimer(name, profileMemory, timer.Parent);
        ActiveTimer = timer;
        timer.StartInternal();

        return timer;
    }

    public void StopTimer()
    {
        var timer = ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();
        ActiveTimer = timer.Parent;
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

    public class EventsAllocator
    {
        public const int SegmentSize = 1024 * 4;

        public ProfilerEvent[][] Segments = [];
        public int NextIndex;

        public ref ProfilerEvent GetEvent(int index)
        {
            int segmentIndex = index / SegmentSize;

            return ref Segments[segmentIndex][index - segmentIndex * SegmentSize];
        }

        public void Alloc(out ProfilerEvent[] array, out int index)
        {
            int i = NextIndex++;
            var segments = Segments;

            if (i == segments.Length * SegmentSize)
                segments = ExpandCapacity();

            int segmentIndex = i / SegmentSize;

            array = segments[segmentIndex];
            index = i - segmentIndex * SegmentSize;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        ProfilerEvent[][] ExpandCapacity()
        {
            // TODO: Add event for allocating new segment

            ref var segments = ref Segments;
            int newSegCount = segments.Length + 1;

            Array.Resize(ref segments, newSegCount);

            segments[^1] = new ProfilerEvent[SegmentSize];

            return segments;
        }
    }

    [ProtoContract]
    public class GroupEventsRecording
    {
        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public ProfilerEventsSegment[] EventSegments;
        [ProtoMember(3)] public int EventCount;
        [ProtoMember(4)] public int[] FrameStartEventIndices;
        [ProtoMember(5)] public int[] FrameEndEventIndices;
        [ProtoMember(6)] public int[] OutlierFrames;

        public ProfilerEventEnumerator AllEvents => new ProfilerEventEnumerator(EventSegments, EventCount);

        public GroupEventsRecording(string name, EventsAllocator events, int[] frameStartIndices, int[] frameEndIndices, int[] outlierFrames)
        {
            Name = name;
            FrameStartEventIndices = frameStartIndices;
            FrameEndEventIndices = frameEndIndices;
            OutlierFrames = outlierFrames;
            EventCount = events.NextIndex;

            var segments = events.Segments;

            int numSegments = (EventCount + EventsAllocator.SegmentSize - 1) / EventsAllocator.SegmentSize;
            EventSegments = new ProfilerEventsSegment[numSegments];

            for (int s = 0; s < EventSegments.Length; s++)
            {
                var segmentEvents = segments[s];
                int endIndexInSegment = Math.Min(segmentEvents.Length - 1, (EventCount - 1) - s * SegmentSize);
                long endTime = segmentEvents[endIndexInSegment].EndTime;

                // The EndTime of the last event is not usually the end bounds of
                // the segment. The parent events end after the children but
                // come before them in the array.

                if (segmentEvents[endIndexInSegment].Depth != 0)
                {
                    for (int i = endIndexInSegment - 1; i >= 0; i--)
                    {
                        ref var e = ref segmentEvents[i];

                        if (e.EndTime > endTime)
                            endTime = e.EndTime;

                        if (e.Depth == 0)
                            break;
                    }
                }

                EventSegments[s] = new ProfilerEventsSegment {
                    Events = segmentEvents,
                    StartTime = segmentEvents[0].StartTime,
                    EndTime = endTime
                };
            }
        }

        public GroupEventsRecording()
        {
            Name = "";
            EventSegments = [];
            FrameStartEventIndices = [];
            FrameEndEventIndices = [];
            OutlierFrames = [];
        }

        public int SegmentSize => EventsAllocator.SegmentSize;

        public ref ProfilerEvent GetEvent(int index)
        {
            int segmentIndex = index / EventsAllocator.SegmentSize;

            return ref EventSegments[segmentIndex].Events[index - segmentIndex * EventsAllocator.SegmentSize];
        }

        public int GetNumRecordedFrames()
        {
            int numStart = FrameStartEventIndices.Length;
            int numEnd = FrameEndEventIndices.Length;

            if (numStart == 0 || numEnd == 0)
                return 0;

            int firstStart = FrameStartEventIndices[0];
            int firstEnd = FrameEndEventIndices[0];

            if (firstEnd == -1 || GetEvent(firstEnd).EndTime < GetEvent(firstStart).StartTime)
                numEnd--; // Start of first frame is cut off

            int lastStart = FrameStartEventIndices[^1];
            int lastEnd = FrameEndEventIndices[^1];

            if (GetEvent(lastStart).StartTime > GetEvent(lastEnd).EndTime)
                numStart--; // End of last frame is cut off

            return Math.Min(numStart, numEnd);
        }
    }

    object frameLock = new();

    EventsAllocator currentEvents = new();

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

    internal void StartEvent(out ProfilerEvent[] array, out int index)
    {
        currentEvents.Alloc(out array, out index);
    }

    public void BeginFrame()
    {
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

                const int ss = EventsAllocator.SegmentSize;

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
        const int ss = EventsAllocator.SegmentSize;

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

    internal GroupEventsRecording? StopEventRecording()
    {
        lock (frameLock)
        {
            // NOTE: ProfilerTimers can still be ending their events concurrently.

            var recordedEvents = currentEvents;
            currentEvents = new EventsAllocator();

            int startIndex = prevFrameEndEventIndex + 1;
            int endIndex = recordedEvents.NextIndex - 1;

            var eventObjectResolver = Profiler.EventObjectResolver;

            if (eventObjectResolver != null)
            {
                if (endIndex >= startIndex)
                    ResolveObjects(recordedEvents, startIndex, endIndex, eventObjectResolver);

                eventObjectResolver.ClearCache();
            }

            GroupEventsRecording? recording = null;

            if (recordedEvents.NextIndex > 0)
                recording = new GroupEventsRecording(Name, recordedEvents, frameStartEventIndices.ToArray(), frameEndEventIndices.ToArray(), outlierFrameIndices.ToArray());

            prevFrameEndEventIndex = -1;

            return recording;
        }
    }

    static void ResolveObjects(EventsAllocator recordedEvents, int startIndex, int endIndex, IProfilerEventDataObjectResolver objectResolver)
    {
        const int ss = EventsAllocator.SegmentSize;

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

                if (_event.ExtraValue.Type == ProfilerEvent.ExtraValueTypeOption.Object)
                    objectResolver.Resolve(ref _event.ExtraValue);
            }
        }
    }

    internal int NumRecordedFrames => GetNumRecordedFrames(currentEvents);

    public int GetNumRecordedFrames(EventsAllocator events)
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

    #region Start / Stop

    public static ProfilerTimer Start(int index, string name, bool profileMemory, ProfilerEvent.ExtraData extraData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(index, name, profileMemory);

        group.ActiveTimer = timer;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(ProfilerKey key, bool profileMemory, ProfilerEvent.ExtraData extraData)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(key, profileMemory);

        group.ActiveTimer = timer;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(string name, bool profileMemory, ProfilerEvent.ExtraData extraData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, profileMemory);

        group.ActiveTimer = timer;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(int index, string name)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(index, name, profileMemory: true);

        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Start(string name)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, profileMemory: true);

        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Start(ProfilerKey key, bool profileMemory = true)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(key, profileMemory);

        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Start(string name, bool profileMemory = true)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, profileMemory);

        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Restart(int index, string name, bool profileMemory = true)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(index, name, profileMemory, timer.Parent);
        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Restart(ProfilerKey key, bool profileMemory = true)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(key, profileMemory, timer.Parent);
        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Restart(string name, bool profileMemory = true)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(name, profileMemory, timer.Parent);
        group.ActiveTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Restart(string name)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.RestartTimer(name, profileMemory: true);

        return timer;
    }

    public static void Stop()
    {
        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        group.StopTimer();
    }

    public static void AddTimeValue(string name, long elapsedTicks)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, false);
        timer.AddElapsedTicks(elapsedTicks);
    }

    public static ProfilerTimer OpenTimer(int groupId, string name)
    {
        Assert.NotNull(name, "Name must not be null");

        if (!profilerGroupsById.TryGetValue(groupId, out var group))
            throw new ArgumentException("Invalid groupId.", nameof(groupId));

        var timer = group.GetOrCreateTimer(name, false);

        return timer;
    }

    [Conditional("DEBUG")]
    public static void StartDebug(string name, bool profileMemory = true) => Start(name, profileMemory);

    [Conditional("DEBUG")]
    public static void StopDebug() => Stop();

    public static void UnwindToDepth(int depth)
    {
        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        while (group.ActiveTimer != null && group.ActiveTimer.Depth > depth)
            group.StopTimer();
    }

    #endregion

    #region Group sorting

    public static void SetSortingGroupForCurrentThread(string sortingGroup, int orderInGroup = 0)
    {
        var group = GetOrCreateGroupForCurrentThread();
        group.SortingGroup = sortingGroup;
        group.OrderInSortingGroup = orderInGroup;
    }

    public static void SetIsRealtimeThread(bool isRealtime)
    {
        var group = GetOrCreateGroupForCurrentThread();
        group.IsRealtimeThread = isRealtime;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProfilerTimer? GetThreadTimer([CallerMemberName] string name = null!)
    {
        return ThreadGroup?.GetTimer(name);
    }

    public static ProfilerTimer GetOrCreateThreadTimer([CallerMemberName] string name = null!, bool profileMemory = true)
    {
        Assert.NotNull(name, "Name must not be null");

        return GetOrCreateGroupForCurrentThread().GetOrCreateTimer(name, profileMemory);
    }

    public static ProfilerGroup? GetGroupForCurrentThread() => ThreadGroup;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ProfilerGroup CreateGroupForCurrentThread()
    {
        var currentThread = Thread.CurrentThread;
        var tg = new ProfilerGroup(currentThread.Name!, currentThread);

        ThreadGroup = tg;

        lock (profilerGroupsById)
            // NOTE: This can replace old entries from threads that have stopped and had
            // their ID reused. Thread ID's are only unique among running threads not over
            // all threads that have run during an application cycle.
            profilerGroupsById[tg.ID] = tg;

        return tg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public static void SetEventObjectResolver(IProfilerEventDataObjectResolver? objectResolver)
    {
        EventObjectResolver = objectResolver;
    }

    public static void StartEventRecording(int? numFrames = null, Action<ProfilerEventsRecording>? completedCallback = null)
    {
        if (isRecordingEvents) throw new InvalidOperationException("Event recording has already started.");

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
    }

    public static void EndOfFrame()
    {
        frameIndex++;

        if (!isRecordingEvents || !numFramesToRecord.HasValue || ThreadGroup == null)
            return;

        if (ThreadGroup.NumRecordedFrames < numFramesToRecord.Value)
            return;

        var recording = StopEventRecording();

        numFramesToRecord = null;
        recordingCompletedCallback?.Invoke(recording);
        recordingCompletedCallback = null;
    }

    public static ProfilerEventsRecording StopEventRecording()
    {
        if (!isRecordingEvents) throw new InvalidOperationException("Event recording has not yet been started.");

        lock (profilerGroupsById)
        {
            if (!isRecordingEvents)
                return null!;

            var groups = new List<ProfilerGroup>(profilerGroupsById.Count);
            var groupsRecordings = new List<(int, ProfilerGroup.GroupEventsRecording)>(profilerGroupsById.Count);

            foreach (var item in profilerGroupsById)
            {
                var events = item.Value.StopEventRecording();

                if (events != null)
                {
                    groups.Add(item.Value);
                    groupsRecordings.Add((item.Key, events));
                }
            }

            isRecordingEvents = false;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddEvent(string name, ProfilerEvent.ExtraData extraData)
    {
        if (!isRecordingEvents)
            return;

        // Allows better inlining of AddEvent
        AddEventInternal(name).ExtraValue = extraData;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        var group = GetOrCreateGroupForCurrentThread();
        group.StartEvent(out var array, out int index);

        var nameKey = ProfilerKeyCache.GetOrAdd(name);

        ref var _event = ref array[index];
        _event.NameKey = nameKey.GlobalIndex;
        _event.Flags = ProfilerEvent.EventFlags.SinglePoint;
        _event.StartTime = _event.EndTime = Stopwatch.GetTimestamp();
        _event.MemoryBefore = _event.MemoryAfter = 0;
        _event.Depth = group.ActiveTimer?.Depth ?? 0;
        return ref _event;
    }
}

public interface IProfilerEventDataObjectResolver
{
    void Resolve(ref ProfilerEvent.ExtraData data);

    void ResolveNonCached(ref ProfilerEvent.ExtraData data);

    void ClearCache();
}

static class ProfilerKeyCache
{
    static readonly object lockObj = new();
    static readonly Dictionary<string, int> namesToKeys = [];
    static readonly Dictionary<int, string> keysToNames = [];
    static int keyGenerator = -1;

    public static ProfilerKey GetOrAdd(string name, ProfilerGroup? group = null)
    {
        return GetOrAdd(name, group, out _);
    }

    public static ProfilerKey GetOrAdd(string name, ProfilerGroup? group, out bool existing)
    {
        int key;

        if (group != null && group.LocalKeyCache.TryGetValue(name, out key))
        {
            existing = true;
            return new ProfilerKey(key);
        }

        lock (lockObj)
        {
            if (!(existing = namesToKeys.TryGetValue(name, out key)))
            {
                key = keyGenerator--;
                namesToKeys.Add(name, key);
                keysToNames.Add(key, name);
            }
        }

        if (group != null)
        {
            group.LocalKeyCache.Add(name, key);
            existing = false;
        }

        return new ProfilerKey(key);
    }

    public static bool TryGet(string name, out ProfilerKey key, ProfilerGroup? group = null)
    {
        int index;

        if (group != null && group.LocalKeyCache.TryGetValue(name, out index))
        {
            key = new ProfilerKey(index);
            return true;
        }

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

[ProtoContract]
public class ProfilerEventsRecording
{
    [ProtoMember(1)] public string SessionName;
    [ProtoMember(2)] public DateTime StartTime;
    [ProtoMember(3)] public int NumFrames;
    [ProtoMember(4)] public Dictionary<int, ProfilerGroup.GroupEventsRecording> Groups;
    [ProtoMember(5)] public Dictionary<int, string> EventStrings;
    [ProtoMember(6)] public Dictionary<int, string> DataStrings;
    [ProtoMember(7)] public Dictionary<int, RefObjWrapper> DataObjects;

    public ProfilerEventsRecording(DateTime startTime, int numFrames, (int GroupId, ProfilerGroup.GroupEventsRecording Recording)[] groups)
    {
        SessionName = "";
        StartTime = startTime;
        NumFrames = numFrames;
        Groups = groups.ToDictionary(k => k.GroupId, e => e.Recording);
        EventStrings = ProfilerKeyCache.GetStrings();
        DataStrings = GeneralStringCache.GetStrings();
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
