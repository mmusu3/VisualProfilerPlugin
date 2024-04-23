using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace AdvancedProfiler;

public struct ProfilerEvent
{
    [Flags]
    public enum OptionFlags : byte
    {
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

    [StructLayout(LayoutKind.Explicit)]
    public struct ExtraValueUnion
    {
        [FieldOffset(0)]
        public long LongValue;

        [FieldOffset(0)]
        public double DoubleValue;

        [FieldOffset(0)]
        public float FloatValue;

        public ExtraValueUnion(long value)
        {
            LongValue = value;
            Unsafe.SkipInit(out DoubleValue);
            Unsafe.SkipInit(out FloatValue);
        }

        public ExtraValueUnion(double value)
        {
            Unsafe.SkipInit(out LongValue);
            DoubleValue = value;
            Unsafe.SkipInit(out FloatValue);
        }

        public ExtraValueUnion(float value)
        {
            Unsafe.SkipInit(out LongValue);
            Unsafe.SkipInit(out DoubleValue);
            FloatValue = value;
        }
    }

    public struct ExtraData
    {
        public ExtraValueTypeOption Type;
        public ExtraValueUnion Value;
        public object? Object;
        public string? Format;

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

    public string Name;
    public long StartTime;
    public long EndTime;
    public OptionFlags Options;
    public long MemoryBefore;
    public long MemoryAfter;
    public int Depth;
    public ExtraValueTypeOption ExtraValueType;
    public ExtraValueUnion ExtraValue;
    public object? ExtraObject;
    public string? ExtraValueFormat;

    // TODO: Event chains for async task tracking
    // public int Next;

    public readonly bool MemoryTracked => (Options & OptionFlags.MemoryTracked) != 0;
    public readonly bool IsSinglePoint => (Options & OptionFlags.SinglePoint) != 0;
    public readonly TimeSpan ElapsedTime => ProfilerTimer.TimeSpanFromTimestampTicks(EndTime - StartTime);
}

public sealed class ProfilerTimer : IDisposable
{
    public readonly string Name;

    long startTimestamp;
    long elapsedTicks;

    public bool IsRunning => isRunning;
    bool isRunning;
    bool wasRun;

    int eventIndex = -1;

    readonly ProfilerGroup group;
    public readonly ProfilerTimer? Parent;
    public readonly bool ProfileMemory;

    public readonly int Depth;

    public IReadOnlyList<ProfilerTimer?> SubTimers => subTimers;
    internal ProfilerTimer?[] subTimers;

    internal readonly Dictionary<string, int> subTimersMap;

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

    public int[] InvokeCounts;
    int prevInvokeCount;

    const long TicksPerMicrosecond = 10;
    const long TicksPerMillisecond = 10000;
    const long TicksPerSecond = TicksPerMillisecond * 1000;
    static double tickFrequency = (double)TicksPerSecond / Stopwatch.Frequency;

    public TimeSpan ElapsedTime => TimeSpanFromTimestampTicks(elapsedTicks);

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

    internal ProfilerTimer(string name, bool profileMemory, ProfilerGroup group, ProfilerTimer? parent)
    {
        Name = name;
        ProfileMemory = profileMemory;
        this.group = group;

        Assert.False(parent == this);

        Parent = parent;

        if (parent != null)
            Depth = parent.Depth + 1;

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

    public ProfilerTimer? FindSubTimer(string name)
    {
        if (!subTimersMap.TryGetValue(name, out int timerIndex))
            return null;

        return subTimers[timerIndex];
    }

    internal void StartEvent(long startTime, long gcMemory)
    {
        ref var _event = ref group.StartEvent(out eventIndex);
        _event.Name = Name;
        _event.Depth = Depth;
        _event.MemoryBefore = _event.MemoryAfter = gcMemory;
        _event.Options = ProfileMemory ? ProfilerEvent.OptionFlags.MemoryTracked : 0;
        _event.StartTime = _event.EndTime = startTime;
        _event.ExtraValueType = ProfilerEvent.ExtraValueTypeOption.None;
        _event.ExtraValue = default;
        _event.ExtraObject = null;
        _event.ExtraValueFormat = null;
    }

    internal void StopEvent(long endTime, long gcMemory)
    {
        ref var _event = ref group.EndEvent(eventIndex);
        _event.EndTime = endTime;
        _event.MemoryAfter = gcMemory;
        eventIndex = -1;
    }

    public void Start()
    {
        Assert.NotNull(group);
        Assert.False(group.activeTimer == this);

        group.activeTimer = this;

        StartInternal(default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StartInternal()
    {
        StartInternal(default);
    }

    internal void StartInternal(ProfilerEvent.ExtraData extraValueData)
    {
        if (isRunning)
        {
            ThrowTimerAlreadyRunning();

            [DoesNotReturn]
            //[StackTraceHidden]
            static void ThrowTimerAlreadyRunning() => throw new InvalidOperationException("Timer is already running");
        }

        isRunning = true;
        wasRun = true;

        ref var _event = ref Unsafe.NullRef<ProfilerEvent>();

        if (Profiler.IsRecordingEvents)
        {
            _event = ref group.StartEvent(out eventIndex);
            _event.Name = Name;
            _event.Depth = Depth;
            _event.ExtraValueType = extraValueData.Type;
            _event.ExtraValue = extraValueData.Value;
            _event.ExtraObject = extraValueData.Object;
            _event.ExtraValueFormat = extraValueData.Format;
        }
        else
        {
            eventIndex = -1;
        }

        if (ProfileMemory)
        {
            MemoryBefore = GC.GetAllocatedBytesForCurrentThread();
#if NET7_0_OR_GREATER
            GCTimeBefore = GC.GetTotalPauseDuration();
#endif
        }

        if (eventIndex != -1)
        {
            _event.Options = ProfileMemory ? ProfilerEvent.OptionFlags.MemoryTracked : 0;
            _event.MemoryBefore = _event.MemoryAfter = MemoryBefore;
        }

        prevInvokeCount++;

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

        if (Profiler.IsRecordingEvents)
        {
            ref var _event = ref Unsafe.NullRef<ProfilerEvent>();

            if (eventIndex != -1)
            {
                _event = ref group.EndEvent(eventIndex);
                _event.EndTime = Stopwatch.GetTimestamp();
                _event.MemoryAfter = ProfileMemory ? GC.GetAllocatedBytesForCurrentThread() : 0;
            }

            _event = ref group.StartEvent(out eventIndex);
            _event.Name = Name;
            _event.Depth = Depth;

            if (ProfileMemory)
            {
                _event.Options = ProfilerEvent.OptionFlags.MemoryTracked;
                _event.MemoryBefore = _event.MemoryAfter = GC.GetAllocatedBytesForCurrentThread();
            }
            else
            {
                _event.Options = 0;
                _event.MemoryBefore = _event.MemoryAfter = 0;
            }

            _event.StartTime = _event.EndTime = Stopwatch.GetTimestamp();

            _event.ExtraValueType = ProfilerEvent.ExtraValueTypeOption.None;
            _event.ExtraValue = default;
            _event.ExtraObject = null;
            _event.ExtraValueFormat = null;
        }
    }

    void UnwindToDepth()
    {
        while (group.activeTimer != null && group.activeTimer.Depth > Depth)
            group.StopTimer();
    }

    public void Stop()
    {
        StopInternal();

        Assert.NotNull(group);
        Assert.True(group.activeTimer == this);

        group.activeTimer = Parent;
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
            // Get post memory usage
            MemoryAfter = GC.GetAllocatedBytesForCurrentThread();

            long memDelta = MemoryAfter - MemoryBefore;
            InclusiveMemoryDelta += memDelta;

#if NET7_0_OR_GREATER
            GCTimeAfter = GC.GetTotalPauseDuration();
            GCTimeDelta = GCTimeAfter - GCTimeBefore;
#endif
        }

        if (eventIndex != -1 && Profiler.IsRecordingEvents)
        {
            ref var _event = ref group.EndEvent(eventIndex);
            _event.EndTime = endTimestamp;
            _event.MemoryAfter = MemoryAfter;
        }
    }

    public void AddElapsedTicks(long elapsedTicks)
    {
        Assert.True(elapsedTicks >= 0);

        this.elapsedTicks += elapsedTicks;
        prevInvokeCount++;
        wasRun = true;
    }

    public void EndFrame()
    {
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

            timer.EndFrame();
        }

        Assert.True(wasRun || !subTimerWasRun);
        //Assert.True(elapsedTicks >= subTimerTicks);

        //TimeExclusive -= subTimerTicks;
        // GPU times are not behaving
        TimeExclusive = Math.Max(0, TimeExclusive - subTimerTicks);

        InclusiveTimes[CurrentIndex] = TimeInclusive;
        ExclusiveTimes[CurrentIndex] = TimeExclusive;

        InclusiveMemoryDeltas[CurrentIndex] = InclusiveMemoryDelta;
        ExclusiveMemoryDeltas[CurrentIndex] = ExclusiveMemoryDelta;

        if (InclusiveMemoryDelta > 0)
            InclusiveMemoryTotal += InclusiveMemoryDelta;

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

    internal ProfilerTimer CreateSubTimer(int index, string name, bool profileMemory)
    {
        var timer = new ProfilerTimer(name, profileMemory, group, this);

        if (index >= subTimers.Length)
            Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(name, index);

        return timer;
    }

    internal ProfilerTimer CreateSubTimer(string name, bool profileMemory)
    {
        var timer = new ProfilerTimer(name, profileMemory, group, this);

        int index;

        if (!subTimersMap.TryGetValue(name, out index))
            index = subTimers.Length;

        if (index >= subTimers.Length)
            Array.Resize(ref subTimers, index + 1);

        subTimers[index] = timer;
        subTimersMap.Add(name, index);

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
        var timer = FindSubTimer(name) ?? CreateSubTimer(name, profileMemory);

        return timer;
    }

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

    internal ProfilerTimer? activeTimer;

    public readonly string Name;
    public readonly int ID;
    public readonly Thread? Thread;

    public string? SortingGroup;
    public int OrderInSortingGroup;
    public bool IsRealtimeThread = true;

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

    public ProfilerTimer? GetRootTimer(string name)
    {
        for (int i = 0; i < rootTimers.Length; i++)
        {
            if (rootTimers[i]?.Name == name)
                return rootTimers[i];
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer? GetTimer(string name)
        => activeTimer != null ? activeTimer.FindSubTimer(name) : GetRootTimer(name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(string name, bool profileMemory)
        => GetOrCreateTimer(name, profileMemory, activeTimer);

    internal ProfilerTimer GetOrCreateTimer(string name, bool profileMemory, ProfilerTimer? parentTimer)
    {
        if (parentTimer != null)
        {
            var timer = parentTimer.FindSubTimer(name);

            if (timer == null)
            {
                timer = parentTimer.CreateSubTimer(name, profileMemory);
                timers.Add(timer);
            }

            return timer;
        }
        else
        {
            return GetRootTimer(name) ?? CreateRootTimer(name, profileMemory);
        }
    }

    ProfilerTimer CreateRootTimer(string name, bool profileMemory)
    {
        var timer = new ProfilerTimer(name, profileMemory, this, null);
        int index = rootTimers.Length;

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProfilerTimer GetOrCreateTimer(int index, string name, bool profileMemory)
        => GetOrCreateTimer(index, name, profileMemory, activeTimer);

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

    ProfilerTimer CreateRootTimer(int index, string name, bool profileMemory)
    {
        var timer = new ProfilerTimer(name, profileMemory, group: this, parent: null);

        Array.Resize(ref rootTimers, index + 1);

        rootTimers[index] = timer;
        timers.Add(timer);

        return timer;
    }

    public ProfilerTimer StartTimer(string name, bool profileMemory, ProfilerEvent.ExtraData extraValueData)
    {
        var timer = GetOrCreateTimer(name, profileMemory);
        activeTimer = timer;
        timer.StartInternal(extraValueData);

        return timer;
    }

    public ProfilerTimer StartTimer(string name, bool profileMemory)
    {
        var timer = GetOrCreateTimer(name, profileMemory, activeTimer);
        activeTimer = timer;
        timer.StartInternal();

        return timer;
    }

    public ProfilerTimer RestartTimer(string name, bool profileMemory)
    {
        var timer = activeTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();
        timer = GetOrCreateTimer(name, profileMemory, timer.Parent);
        activeTimer = timer;
        timer.StartInternal();

        return timer;
    }

    public void StopTimer()
    {
        var timer = activeTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();
        activeTimer = timer.Parent;
    }

    public void EndFrame(ResolveProfilerEventObjectDelegate? eventObjectResolver = null)
    {
        if (activeTimer != null) throw new InvalidOperationException($"Profiler group '{Name}' still has an active timer '{activeTimer.Name}'");

        foreach (var item in rootTimers)
            item?.EndFrame();

        if (currentEventIndex == startEventIndexForFrame)
            return;

        if (eventObjectResolver != null)
        {
            int startSegmentIndex = (startEventIndexForFrame - 1) / EventBufferSegmentSize;
            int endSegmentIndex = (currentEventIndex - 1) / EventBufferSegmentSize + 1;

            for (int i = startSegmentIndex; i < endSegmentIndex; i++)
            {
                var segment = Events[i];
                int startEventIndex = startEventIndexForFrame - i * EventBufferSegmentSize;
                int endEventIndex = Math.Min(segment.Length, currentEventIndex - i * EventBufferSegmentSize);

                for (int j = startEventIndex; j < endEventIndex; j++)
                {
                    ref var _event = ref segment[j];

                    if (_event.ExtraValueType == ProfilerEvent.ExtraValueTypeOption.Object)
                        eventObjectResolver(eventObjectsCache, ref _event);
                }
            }
        }

        startEventIndexForFrame = currentEventIndex;
    }

    public void ClearTimers()
    {
        if (activeTimer != null) throw new InvalidOperationException("All timers in this group must be stopped before clearing.");

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

    public const int EventBufferSegmentSize = 1024 * 16;
    public List<ProfilerEvent[]> Events = [];

    public int CurrentEventIndex => currentEventIndex;
    int currentEventIndex;

    int startEventIndexForFrame;

    Dictionary<object, object> eventObjectsCache = [];

    internal void StartEventRecording(long startTime, long gcMemory)
    {
        currentEventIndex = 0;
        startEventIndexForFrame = 0;
        eventObjectsCache.Clear();

        foreach (var item in timers)
        {
            if (item.IsRunning)
                item.StartEvent(startTime, gcMemory);
        }
    }

    internal void StopEventRecording(long endTime, long gcMemory)
    {
        foreach (var item in timers)
        {
            if (item.IsRunning)
                item.StopEvent(endTime, gcMemory);
        }
    }

    internal ref ProfilerEvent StartEvent(out int index)
    {
        if (currentEventIndex == Events.Count * EventBufferSegmentSize)
            Events.Add(new ProfilerEvent[EventBufferSegmentSize]); // TODO: Add event for allocating new buffer

        index = currentEventIndex++;

        int segmentIndex = index / EventBufferSegmentSize;

        return ref Events[segmentIndex][index - segmentIndex * EventBufferSegmentSize];
    }

    internal ref ProfilerEvent EndEvent(int index)
    {
        int segmentIndex = index / EventBufferSegmentSize;

        return ref Events[segmentIndex][index - segmentIndex * EventBufferSegmentSize];
    }

    internal ref ProfilerEvent AddEvent()
    {
        if (currentEventIndex == Events.Count * EventBufferSegmentSize)
            Events.Add(new ProfilerEvent[EventBufferSegmentSize]);

        int index = currentEventIndex++;
        int segmentIndex = index / EventBufferSegmentSize;

        return ref Events[segmentIndex][index - segmentIndex * EventBufferSegmentSize];
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

    public static bool IsRecordingEvents => isRecordingEvents;
    static bool isRecordingEvents;

    public static bool IsTimerActive()
    {
        var group = GetOrCreateGroupForCurrentThread();
        return group.activeTimer != null;
    }

    public static ProfilerTimer Start(int index, string name, bool profileMemory, ProfilerEvent.ExtraData extraValueData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(index, name, profileMemory);

        group.activeTimer = timer;

        timer.StartInternal(extraValueData);

        return timer;
    }

    public static ProfilerTimer Start(string name, bool profileMemory, ProfilerEvent.ExtraData extraValueData)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, profileMemory);

        group.activeTimer = timer;

        timer.StartInternal(extraValueData);

        return timer;
    }

    public static ProfilerTimer Start(int index, string name)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(index, name, profileMemory: true);

        group.activeTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Start(string name)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, profileMemory: true);

        group.activeTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Start([CallerMemberName] string name = null!, bool profileMemory = true)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = GetOrCreateGroupForCurrentThread();
        var timer = group.GetOrCreateTimer(name, profileMemory);

        group.activeTimer = timer;

        timer.StartInternal();

        return timer;
    }

    public static ProfilerTimer Restart([CallerMemberName] string name = null!, bool profileMemory = true)
    {
        Assert.NotNull(name, "Name must not be null");

        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        var timer = group.activeTimer;

        Assert.NotNull(timer, "Must call Profiler.Start first");

        timer.StopInternal();

        timer = group.GetOrCreateTimer(name, profileMemory, timer.Parent);
        group.activeTimer = timer;

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
    public static void StartDebug([CallerMemberName] string name = null!, bool profileMemory = true) => Start(name, profileMemory);

    [Conditional("DEBUG")]
    public static void StopDebug() => Stop();

    public static void UnwindToDepth(int depth)
    {
        var group = ThreadGroup;

        Assert.NotNull(group, "Must call Profiler.Start first");

        while (group.activeTimer != null && group.activeTimer.Depth > depth)
            group.StopTimer();
    }

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

    public static void EndFrameForCurrentThread(ResolveProfilerEventObjectDelegate? eventObjectResolver = null)
    {
        ThreadGroup?.EndFrame(eventObjectResolver);
    }

    public static void EndFrameForThreadID(int threadId)
    {
        lock (profilerGroupsById)
        {
            if (profilerGroupsById.TryGetValue(threadId, out var group))
                group.EndFrame();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProfilerTimer? GetThreadTimer(int index)
    {
        var group = ThreadGroup;

        if (group == null)
            return null;

        ProfilerTimer? timer;

        if (group.activeTimer != null)
            timer = group.activeTimer.subTimers[index];
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
            profilerGroupsById[currentThread.ManagedThreadId] = tg;

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

    public static void StartEventRecording()
    {
        if (isRecordingEvents) throw new InvalidOperationException("Event recording has already started.");

        isRecordingEvents = true;

        long memory = GC.GetAllocatedBytesForCurrentThread();
        long startTime = Stopwatch.GetTimestamp();

        lock (profilerGroupsById)
        {
            foreach (var item in profilerGroupsById.Values)
                item.StartEventRecording(startTime, memory);
        }
    }

    public static void StopEventRecording()
    {
        if (!isRecordingEvents) throw new InvalidOperationException("Event recording has not yet been started.");

        long endTime = Stopwatch.GetTimestamp();
        long memory = GC.GetAllocatedBytesForCurrentThread();

        lock (profilerGroupsById)
        {
            foreach (var item in profilerGroupsById.Values)
                item.StopEventRecording(endTime, memory);
        }

        isRecordingEvents = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddEvent(string name, ProfilerEvent.ExtraData extraValueData)
    {
        if (!isRecordingEvents)
            return;

        // Allows better inlining of AddEvent
        ref var _event = ref AddEventInternal(name);

        _event.ExtraValueType = extraValueData.Type;
        _event.ExtraValue = extraValueData.Value;
        _event.ExtraObject = extraValueData.Object;
        _event.ExtraValueFormat = extraValueData.Format;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddEvent(string name)
    {
        if (!isRecordingEvents)
            return;

        // Allows better inlining of AddEvent
        ref var _event = ref AddEventInternal(name);

        _event.ExtraValueType = ProfilerEvent.ExtraValueTypeOption.None;
        _event.ExtraValue = default;
        _event.ExtraObject = null;
        _event.ExtraValueFormat = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref ProfilerEvent AddEventInternal(string name)
    {
        var group = GetOrCreateGroupForCurrentThread();
        ref var _event = ref group.AddEvent();
        _event.Name = name;
        _event.Options = ProfilerEvent.OptionFlags.SinglePoint;
        _event.StartTime = _event.EndTime = Stopwatch.GetTimestamp();
        _event.MemoryBefore = _event.MemoryAfter = 0;
        _event.Depth = group.activeTimer?.Depth ?? 0;
        return ref _event;
    }
}

public delegate void ResolveProfilerEventObjectDelegate(Dictionary<object, object> cache, ref ProfilerEvent _event);
