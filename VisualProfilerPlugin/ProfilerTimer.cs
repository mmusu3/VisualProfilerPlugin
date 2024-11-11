using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using VRageMath;

namespace VisualProfiler;

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

    internal ProfilerTimer(string name, ProfilerKey key, ProfilerTimerOptions options, ProfilerGroup group, ProfilerTimer? parent, int depth)
    {
        Name = name;
        Key = key;
        this.options = options;
        this.group = group;

        Assert.False(parent == this);

        Parent = parent;
        Depth = depth;

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
        Assert.True(Depth == group.CurrentDepth + 1);

        group.ActiveTimer = this;
        group.CurrentDepth = Depth;

        StartInternal(default);
    }

    internal void StartInternal(in ProfilerEvent.ExtraData extraData)
    {
        if (!Profiler.IsEnabled || (Parent != null && Parent.state == State.StartedDisabled))
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
        if (!Profiler.IsEnabled || (Parent != null && Parent.state == State.StartedDisabled))
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
        group.CurrentDepth--;
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

        Assert.True(!subTimerWasRun || WasRun);
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
    internal ProfilerTimer CreateSubTimer(int index, string name, ProfilerTimerOptions options, int depth)
    {
        ProfilerKey key;

        if (!group.LocalKeyCache.TryGetValue(name, out key))
        {
            key = ProfilerKeyCache.GetOrAdd(name);
            group.LocalKeyCache.Add(name, key);
        }

        var timer = new ProfilerTimer(name, key, options, group, this, depth);

        if (index >= subTimers.Length)
            Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(key.GlobalIndex, timer);

        return timer;
    }

    internal ProfilerTimer CreateSubTimer(ProfilerKey key, ProfilerTimerOptions options, int depth)
    {
        var name = ProfilerKeyCache.GetName(key);

        return CreateSubTimer(name, key, options, depth);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ProfilerTimer CreateSubTimer(string name, ProfilerKey key, ProfilerTimerOptions options, int depth)
    {
        var timer = new ProfilerTimer(name, key, options, group, this, depth);
        int index = subTimers.Length;

        Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(key.GlobalIndex, timer);

        return timer;
    }

    public ProfilerTimer GetOrCreateSubTimer(int index, string name, ProfilerTimerOptions options, int depth)
    {
        var timer = subTimers[index];

        if (timer != null)
            return timer;

        return CreateSubTimer(index, name, options, depth);
    }

    public ProfilerTimer GetOrCreateSubTimer(string name, ProfilerTimerOptions options, int depth, out bool existing)
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

        timer = CreateSubTimer(name, key, options, depth);

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
        group.CurrentDepth--;
    }

    public override string ToString() => $"Timer: {Name}, Profile Memory: {ProfileMemory}";
}
