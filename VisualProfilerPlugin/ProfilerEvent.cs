using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public readonly double ElapsedMicroseconds => ProfilerTimer.MicrosecondsFromTicks(EndTime - StartTime);
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

public struct ProfilerEventHandle : IDisposable
{
    internal ProfilerEvent[] Array;
    internal int EventIndex;
    internal int Depth;

    public readonly void Dispose()
    {
        long endTime = Stopwatch.GetTimestamp();

        if (Array != null)
        {
            ref var _event = ref Array[EventIndex];

            bool profileMemory = (_event.Flags & ProfilerEvent.EventFlags.MemoryTracked) != 0;
            long memory = profileMemory ? GC.GetAllocatedBytesForCurrentThread() : 0;

            _event.EndTime = endTime;
            _event.MemoryAfter = memory;
        }

        var group = Profiler.GetGroupForCurrentThread();

        if (group != null)
        {
            Assert.True(Depth == group.CurrentDepth);

            group.CurrentDepth = Depth - 1;
        }
    }
}
