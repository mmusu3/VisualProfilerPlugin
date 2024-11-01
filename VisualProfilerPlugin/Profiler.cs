using System;
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
        Float = 4,
        ObjectAndCategory = 5
    }

    public enum EventCategory
    {
        Other,
        Wait,
        Save,
        Load,
        Physics,
        Network,
        World,
        Grids,
        Blocks,
        Characters,
        FloatingObjects,
        Scripts,
        Mods,

        CategoryCount
    }

    [ProtoContract]
    public struct ExtraValueUnion
    {
        [ProtoMember(1)] public long DataField;
        [ProtoIgnore] public readonly long LongValue => DataField;
        [ProtoIgnore] public double DoubleValue => Unsafe.As<long, double>(ref DataField);
        [ProtoIgnore] public float FloatValue => Unsafe.As<long, float>(ref DataField);
        [ProtoIgnore] public readonly EventCategory CategoryValue => (EventCategory)(DataField >> 32);

        public ExtraValueUnion(long value)
        {
            DataField = value;
        }

        public ExtraValueUnion(double value)
        {
            DataField = 0;
            Unsafe.As<long, double>(ref DataField) = value;
        }

        public ExtraValueUnion(float value)
        {
            DataField = 0;
            Unsafe.As<long, float>(ref DataField) = value;
        }

        public ExtraValueUnion(EventCategory category)
        {
            DataField = (long)category << 32;
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
            readonly get => new ObjectId((int)Value.DataField);
            set => Value.DataField = (Value.DataField & ~0xFFFFFFFFL) | (long)value.ID;
        }

        [ProtoMember(4)] public string? Format;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(object? obj, string? format = null)
        {
            Type = ExtraValueTypeOption.Object;
            Value = default;
            Object = obj;
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(EventCategory category, object? obj = null, string? format = null)
        {
            Type = ExtraValueTypeOption.ObjectAndCategory;
            Value = new(category);
            Object = obj;
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(long value, string? format = null)
        {
            Type = ExtraValueTypeOption.Long;
            Value = new(value);
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(double value, string? format = null)
        {
            Type = ExtraValueTypeOption.Double;
            Value = new(value);
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(float value, string? format = null)
        {
            Type = ExtraValueTypeOption.Float;
            Value = new(value);
            Format = format;
        }
    }

    public readonly string Name => ProfilerKeyCache.GetName(new(NameKey));

    [ProtoMember(1)] public int NameKey;
    [ProtoMember(2)] public EventFlags Flags; // TODO: Move ExtraValueTypeOption here as byte size
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
    public readonly double ElapsedMcroseconds => ProfilerTimer.MicrosecondsFromTicks(EndTime - StartTime);
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

[Flags]
public enum ProfilerTimerOptions
{
    None = 0,
    ProfileMemory = 1
}

public sealed class ProfilerTimer : IDisposable
{
    enum State
    {
        Stopped,
        Running,
        WasRun,
        StartedDisabled
    }

    public struct GCCountsInfo
    {
        public Vector3I Before;
        public Vector3I After;
        public Vector3I Inclusive;
        public Vector3I Exclusive;
        public Vector3I CumulativeExclusive;

        internal Vector3I CumulativeInclusiveForChildren;
        internal long FirstChildStartTime;
        internal Vector3I FirstChildBefore;
    }

    public bool IsRunning => state == State.Running;
    public bool WasRun => state == State.WasRun;

    State state;
    int invokeCount;

    readonly ProfilerGroup group;

    ProfilerEvent[]? eventArray;
    int eventIndex = -1;

    public readonly ProfilerKey Key;
    public readonly int Depth;
    ProfilerTimerOptions options;

    public bool ProfileMemory => (options & ProfilerTimerOptions.ProfileMemory) != 0;

    long startTimestamp;
    long elapsedTicks;

    public long MemoryBefore;
    public long MemoryAfter;
    public long InclusiveMemoryDelta;

    public GCCountsInfo GCCounts;

#if NET7_0_OR_GREATER
    public TimeSpan GCTimeBefore;
    public TimeSpan GCTimeAfter;
    public TimeSpan GCTimeDelta;
#endif

    public readonly ProfilerTimer? Parent;
    public readonly string Name;

    public IReadOnlyList<ProfilerTimer?> SubTimers => subTimers;
    internal ProfilerTimer?[] subTimers;

    internal readonly Dictionary<int, ProfilerTimer> subTimersMap;

    public long TimeInclusive;
    public long TimeExclusive;

    public double AverageExclusiveTime;
    public double ExclusiveTimeVariance;

    public long ExclusiveMemoryDelta;

    public int HistoryIndex;

    public int[] InvokeCounts;

    public long[] InclusiveTimes;
    public long[] ExclusiveTimes;

    public long[] InclusiveMemoryDeltas;
    public long[] ExclusiveMemoryDeltas;

    public Vector3I[] GCCountsHistory;

#if NET7_0_OR_GREATER
    public TimeSpan[] GCTimes;
#endif

    static ProfilerKey GCKey = ProfilerKeyCache.GetOrAdd("GC");

    public const int BufferSize = 300;

    const int outlierAveragingSampleRange = 50;
    const int minTicksForOutlier = 1000;
    const double maxOutlierDeviationFraction = 5;

    // Stopwatch freqency is ticks per second
    static readonly double stopwatchToTimeSpanTicks = 10_000_000.0 / Stopwatch.Frequency;
    static readonly double millisecondsPerTick = 1_000.0 / Stopwatch.Frequency;
    static readonly double microsecondsPerTick = 1_000_000.0 / Stopwatch.Frequency;

    public TimeSpan ElapsedTime => TimeSpanFromTimestampTicks(elapsedTicks);

    public static double MillisecondsFromTicks(long ticks) => ticks * millisecondsPerTick;
    public static double MicrosecondsFromTicks(long ticks) => ticks * microsecondsPerTick;
    public static TimeSpan TimeSpanFromTimestampTicks(long ticks) => new TimeSpan(unchecked((long)(ticks * stopwatchToTimeSpanTicks)));

    internal ProfilerTimer(string name, ProfilerKey key, ProfilerTimerOptions options, ProfilerGroup group, ProfilerTimer? parent)
    {
        Name = name;
        Key = key;
        this.options = options;
        this.group = group;

        Assert.False(parent == this);

        Parent = parent;

        if (parent != null)
            Depth = parent.Depth + 1;

        GCCountsHistory = new Vector3I[BufferSize];
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
        if (subTimersMap.TryGetValue(key.GlobalIndex, out var timer))
            return timer;

        return null;
    }

    public void Start()
    {
        Assert.NotNull(group);
        Assert.False(group.ActiveTimer == this);

        group.ActiveTimer = this;

        StartInternal(default);
    }

    internal void StartInternal(ProfilerEvent.ExtraData extraData)
    {
        if (!Profiler.IsEnabled)
        {
            state = State.StartedDisabled;
            return;
        }

        if (IsRunning)
        {
            ThrowTimerAlreadyRunning();

            [DoesNotReturn]
            //[StackTraceHidden]
            static void ThrowTimerAlreadyRunning() => throw new InvalidOperationException("Timer is already running");
        }

        state = State.Running;

        invokeCount++;

        ref var _event = ref Unsafe.NullRef<ProfilerEvent>();

        if (group.IsRealtimeThread || Profiler.IsRecordingEvents)
        {
            _event = ref group.StartEvent(out eventArray, out eventIndex);
            _event.NameKey = Key.GlobalIndex;
            _event.Flags = ProfileMemory ? ProfilerEvent.EventFlags.MemoryTracked : 0;
            _event.MemoryBefore = _event.MemoryAfter = 0;
            _event.Depth = Depth;
            _event.ExtraValue = extraData;

            if (extraData.Type is ProfilerEvent.ExtraValueTypeOption.Object or ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory)
            {
                if (!group.IsRealtimeThread && extraData.Object != null)
                    Profiler.EventObjectResolver?.ResolveNonCached(ref _event.ExtraValue);
            }
        }

        if (ProfileMemory)
        {
            MemoryBefore = GC.GetAllocatedBytesForCurrentThread();

            if (eventIndex != -1)
                _event.MemoryBefore = _event.MemoryAfter = MemoryBefore;

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

        startTimestamp = Stopwatch.GetTimestamp();

        if (eventIndex != -1)
            _event.StartTime = _event.EndTime = startTimestamp;
    }

    public void StartOrSplit()
    {
        if (!Profiler.IsEnabled)
        {
            state = State.StartedDisabled;
            return;
        }

        if (!IsRunning)
        {
            Start();
            return;
        }

        invokeCount++;

        if (!group.IsRealtimeThread && !Profiler.IsRecordingEvents)
            return;

        ref var _event = ref Unsafe.NullRef<ProfilerEvent>();

        if (eventArray != null && eventIndex != -1)
        {
            long endTimestamp = Stopwatch.GetTimestamp();

            _event = ref eventArray[eventIndex];
            _event.EndTime = endTimestamp;

            if (ProfileMemory)
            {
                _event.MemoryAfter = GC.GetAllocatedBytesForCurrentThread();

                EndGCCountsInfo(_event.EndTime);
            }
            else
            {
                _event.MemoryAfter = 0;
            }
        }

        _event = ref group.StartEvent(out eventArray, out eventIndex);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stop()
    {
        StopInternal();

        Assert.NotNull(group);
        Assert.True(group.ActiveTimer == this);

        group.ActiveTimer = Parent;
    }

    internal void StopInternal()
    {
        if (state == State.StartedDisabled)
        {
            state = State.Stopped;
            return;
        }

        if (state != State.Running)
        {
            ThrowTimerNotRunning();

            [DoesNotReturn]
            //[StackTraceHidden]
            static void ThrowTimerNotRunning() => throw new InvalidOperationException("Timer is not running. Must call Start first");
        }

        if (!Profiler.IsEnabled)
        {
            Reset();
            return;
        }

        long endTimestamp = Stopwatch.GetTimestamp();
        elapsedTicks += endTimestamp - startTimestamp;

        state = State.Stopped;

        if (elapsedTicks < 0)
            elapsedTicks = 0;

        if (ProfileMemory)
        {
            MemoryAfter = GC.GetAllocatedBytesForCurrentThread();

            long memDelta = MemoryAfter - MemoryBefore;
            InclusiveMemoryDelta += memDelta;

#if NET7_0_OR_GREATER
            GCTimeAfter = GC.GetTotalPauseDuration();
            GCTimeDelta = GCTimeAfter - GCTimeBefore;
#endif

            if (Profiler.IsRecordingEvents)
                EndGCCountsInfo(endTimestamp);
        }

        if (eventArray != null && eventIndex != -1)
        {
            ref var _event = ref eventArray[eventIndex];
            _event.EndTime = endTimestamp;
            _event.MemoryAfter = MemoryAfter;

            eventArray = null;
            eventIndex = -1;
        }

        state = State.WasRun;
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

        // TODO: Color events based on GC generation

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
        invokeCount++;
        state = State.WasRun;
    }

    public void EndFrame(out bool hasOutliers)
    {
        hasOutliers = false;

        if (!Profiler.IsEnabled)
            return;

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

            if (timer.WasRun)
            {
                subTimerWasRun = true;
                subTimerTicks += timer.elapsedTicks;
                ExclusiveMemoryDelta -= timer.InclusiveMemoryDelta;
            }

            timer.EndFrame(out bool outliers);

            hasOutliers |= outliers;
        }

        Assert.True(WasRun || !subTimerWasRun);
        //Assert.True(elapsedTicks >= subTimerTicks);

        //TimeExclusive -= subTimerTicks;
        TimeExclusive = Math.Max(0, TimeExclusive - subTimerTicks);

        if (WasRun)
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

        InclusiveTimes[HistoryIndex] = TimeInclusive;
        ExclusiveTimes[HistoryIndex] = TimeExclusive;

        InclusiveMemoryDeltas[HistoryIndex] = InclusiveMemoryDelta;
        ExclusiveMemoryDeltas[HistoryIndex] = ExclusiveMemoryDelta;

        GCCountsHistory[HistoryIndex] = GCCounts.CumulativeExclusive;
#if NET7_0_OR_GREATER
        GCTimes[HistoryIndex] = GCTimeDelta;
#endif

        InvokeCounts[HistoryIndex] = invokeCount;

        HistoryIndex++;

        if (HistoryIndex == BufferSize)
            HistoryIndex = 0;

        Reset();
    }

    public void Reset()
    {
        Assert.False(IsRunning);

        elapsedTicks = 0;
        state = State.Stopped;
        startTimestamp = 0;
        invokeCount = 0;
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
            long inclusive = InclusiveTimes[(BufferSize - i + HistoryIndex) % BufferSize];
            averageInclusiveTime += MillisecondsFromTicks(inclusive);

            long exclusive = ExclusiveTimes[(BufferSize - i + HistoryIndex) % BufferSize];
            averageExclusiveTime += MillisecondsFromTicks(exclusive);
        }

        averageInclusiveTime /= averageRange;
        averageExclusiveTime /= averageRange;
    }

    #region Get / Create Timers

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ProfilerTimer CreateSubTimer(int index, string name, ProfilerTimerOptions options)
    {
        ProfilerKey key;

        if (!group.LocalKeyCache.TryGetValue(name, out key))
        {
            key = ProfilerKeyCache.GetOrAdd(name);
            group.LocalKeyCache.Add(name, key);
        }

        var timer = new ProfilerTimer(name, key, options, group, this);

        if (index >= subTimers.Length)
            Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(key.GlobalIndex, timer);

        return timer;
    }

    internal ProfilerTimer CreateSubTimer(ProfilerKey key, ProfilerTimerOptions options)
    {
        var name = ProfilerKeyCache.GetName(key);

        return CreateSubTimer(name, key, options);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ProfilerTimer CreateSubTimer(string name, ProfilerKey key, ProfilerTimerOptions options)
    {
        var timer = new ProfilerTimer(name, key, options, group, this);
        int index = subTimers.Length;

        Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(key.GlobalIndex, timer);

        return timer;
    }

    public ProfilerTimer GetOrCreateSubTimer(int index, string name, ProfilerTimerOptions options)
    {
        var timer = subTimers[index];

        if (timer != null)
            return timer;

        return CreateSubTimer(index, name, options);
    }

    public ProfilerTimer GetOrCreateSubTimer(string name, ProfilerTimerOptions options, out bool existing)
    {
        ProfilerKey key;
        ProfilerTimer? timer;

        if (group.LocalKeyCache.TryGetValue(name, out key))
        {
            timer = FindSubTimer(key);

            if (timer != null)
            {
                existing = true;
                return timer;
            }
        }
        else
        {
            key = ProfilerKeyCache.GetOrAdd(name);
            group.LocalKeyCache.Add(name, key);
        }

        existing = false;

        timer = CreateSubTimer(name, key, options);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Assert.NotNull(group);

        group.UnwindToDepth(Depth); // In case of exceptions
        StopInternal();

        Assert.True(group.ActiveTimer == this);

        group.ActiveTimer = Parent;
    }

    public override string ToString() => $"Timer: {Name}, Profile Memory: {ProfileMemory}";
}

public class ProfilerGroup
{
    internal ProfilerTimer? ActiveTimer;
    internal Dictionary<string, ProfilerKey> LocalKeyCache = [];

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

    internal ProfilerTimer GetOrCreateRootTimer(string name, ProfilerTimerOptions options, out bool existing)
    {
        ProfilerKey key;
        ProfilerTimer? timer;

        if (LocalKeyCache.TryGetValue(name, out key))
        {
            timer = GetRootTimer(key);

            if (timer != null)
            {
                existing = true;
                return timer;
            }
        }
        else
        {
            key = ProfilerKeyCache.GetOrAdd(name);
            LocalKeyCache.Add(name, key);
        }

        existing = false;

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
        => GetOrCreateTimer(name, options, ActiveTimer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ProfilerTimer GetOrCreateTimer(string name, ProfilerTimerOptions options, ProfilerTimer? parentTimer)
    {
        ProfilerTimer? timer;
        bool existing;

        if (parentTimer != null)
        {
            timer = parentTimer.GetOrCreateSubTimer(name, options, out existing);

            if (!existing)
                timers.Add(timer);
        }
        else
        {
            timer = GetOrCreateRootTimer(name, options, out existing);
        }

        return timer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateRootTimer(string name, ProfilerKey key, ProfilerTimerOptions options)
    {
        var timer = new ProfilerTimer(name, key, options, this, null);
        int index = rootTimers.Length;

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(int index, string name, ProfilerTimerOptions options)
        => GetOrCreateTimer(index, name, options, ActiveTimer);

    internal ProfilerTimer GetOrCreateTimer(int index, string name, ProfilerTimerOptions options, ProfilerTimer? parentTimer)
    {
        ProfilerTimer? timer = null;

        if (parentTimer != null)
        {
            if ((uint)index < (uint)parentTimer.subTimers.Length)
                timer = parentTimer.subTimers[index];

            if (timer == null)
            {
                timer = parentTimer.CreateSubTimer(index, name, options);
                timers.Add(timer);
            }
        }
        else
        {
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

        var timer = new ProfilerTimer(name, key, options, group: this, parent: null);

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(ProfilerKey key, ProfilerTimerOptions options)
        => GetOrCreateTimer(key, options, ActiveTimer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ProfilerTimer GetOrCreateTimer(ProfilerKey key, ProfilerTimerOptions options, ProfilerTimer? parentTimer)
    {
        if (parentTimer != null)
            return parentTimer.FindSubTimer(key) ?? CreateSubTimer(parentTimer, key, options);
        else
            return GetRootTimer(key) ?? CreateRootTimer(key, options);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    ProfilerTimer CreateSubTimer(ProfilerTimer parentTimer, ProfilerKey key, ProfilerTimerOptions options)
    {
        var timer = parentTimer.CreateSubTimer(key, options);
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
        var timer = new ProfilerTimer(name, key, options, this, null);
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
        var timer = GetOrCreateTimer(name, options, ActiveTimer);
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
        timer = GetOrCreateTimer(name, options, timer.Parent);
        ActiveTimer = timer;
        timer.StartInternal(default);

        return timer;
    }

    public void StopActiveTimer()
    {
        var timer = ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();
        ActiveTimer = timer.Parent;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UnwindToDepth(int depth)
    {
        while (ActiveTimer != null && ActiveTimer.Depth > depth)
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

        public ref ProfilerEvent Alloc(out ProfilerEvent[] array, out int index)
        {
            int i = NextIndex++;
            int segmentIndex = i / SegmentSize;
            var segments = Segments;

            if (segmentIndex == segments.Length)
                segments = ExpandCapacity();

            array = segments[segmentIndex];
            index = i - segmentIndex * SegmentSize;

            return ref array[index];
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

                if (_event.ExtraValue.Type is ProfilerEvent.ExtraValueTypeOption.Object or ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory)
                {
                    if (_event.ExtraValue.Object != null)
                        objectResolver.Resolve(ref _event.ExtraValue);
                }
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

    public static ProfilerTimer Start(int index, string name, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(index, name, options, group.ActiveTimer);

        group.ActiveTimer = timer;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(ProfilerKey key, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        Assert.True(key.GlobalIndex < 0, "Invalid key");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(key, options, group.ActiveTimer);

        group.ActiveTimer = timer;

        timer.StartInternal(extraData);

        return timer;
    }

    public static ProfilerTimer Start(string name, ProfilerTimerOptions options, ProfilerEvent.ExtraData extraData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, options, group.ActiveTimer);

        group.ActiveTimer = timer;

        timer.StartInternal(extraData);

        return timer;
    }

    [MethodImpl(Inline)]
    public static ProfilerTimer Start(int index, string name) => Start(index, name, ProfilerTimerOptions.ProfileMemory, default);

    [MethodImpl(Inline)]
    public static ProfilerTimer Start(string name) => Start(name, ProfilerTimerOptions.ProfileMemory, default);

    [MethodImpl(Inline)]
    public static ProfilerTimer Start(ProfilerKey key, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory) => Start(key, options, default);

    [MethodImpl(Inline)]
    public static ProfilerTimer Start(string name, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory) => Start(name, options, default);

    public static ProfilerTimer Restart(int index, string name, ProfilerTimerOptions options = ProfilerTimerOptions.ProfileMemory)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.ActiveTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(index, name, options, timer.Parent);

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

        timer = group.GetOrCreateTimer(key, options, timer.Parent);

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

        timer = group.GetOrCreateTimer(name, options, timer.Parent);

        group.ActiveTimer = timer;

        timer.StartInternal(default);

        return timer;
    }

    [MethodImpl(Inline)]
    public static ProfilerTimer Restart(string name)
    {
        return Restart(name, ProfilerTimerOptions.ProfileMemory);
    }

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
        var timer = group.GetOrCreateTimer(name, ProfilerTimerOptions.None, group.ActiveTimer);

        timer.AddElapsedTicks(elapsedTicks);
    }

    public static ProfilerTimer OpenTimer(int groupId, string name)
    {
        Assert.NotNull(name, "Name must not be null");

        if (!profilerGroupsById.TryGetValue(groupId, out var group))
            throw new ArgumentException("Invalid groupId.", nameof(groupId));

        var timer = group.GetOrCreateTimer(name, ProfilerTimerOptions.None, group.ActiveTimer);

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

    [MethodImpl(Inline)]
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

    public static void SetEnabled(bool enabled)
    {
        if (isRecordingEvents) throw new InvalidOperationException("Cannot change profiler enabled state while event recording is in progress.");

        isEnabled = enabled;
    }

    public static void SetEventObjectResolver(IProfilerEventDataObjectResolver? objectResolver)
    {
        EventObjectResolver = objectResolver;
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
    }

    public static void EndOfFrame()
    {
        frameIndex++;

        if (!isEnabled)
            return;

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

            isRecordingEvents = false;

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

[ProtoContract]
public class ProfilerEventsRecording
{
    // TODO: Version number

    [ProtoMember(1)] public string SessionName;
    [ProtoMember(2)] public DateTime StartTime;
    [ProtoMember(3)] public int NumFrames;
    [ProtoMember(4)] public Dictionary<int, ProfilerGroup.GroupEventsRecording> Groups;
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

    public ProfilerEventsRecording(DateTime startTime, int numFrames, (int GroupId, ProfilerGroup.GroupEventsRecording Recording)[] groups)
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
