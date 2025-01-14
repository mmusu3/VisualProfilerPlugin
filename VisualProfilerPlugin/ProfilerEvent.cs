using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using ProtoBuf;
using VRageMath;

namespace VisualProfiler;

[ProtoContract(UseProtoMembersOnly = true)]
public struct ProfilerEvent
{
    [Flags]
    public enum EventFlags : uint
    {
        None = 0,
        MemoryTracked = 1,
        SinglePoint = 2
    }

    public enum DataTypeOption : byte
    {
        None = 0,
        Object = 1,
        Long = 2,
        Double = 3,
        Float = 4,
        ObjectAndCategory = 5
    }

    public enum EventCategory : uint
    {
        Other       = 0,
        Wait        = 1,
        Save        = 2,
        Load        = 3,
        Physics     = 4,
        Network     = 5,
        World       = 6,
        Grids       = 7,
        Blocks      = 8,
        Characters  = 9,
        FloatingObjects = 10,
        Scripts     = 11,
        Mods        = 12,
        Profiler    = 13,

        CategoryCount
    }

    public struct ExtraValueUnion
    {
        public ulong DataField;

        public readonly long LongValue => (long)DataField;
        public double DoubleValue => Unsafe.As<ulong, double>(ref DataField);
        public float FloatValue => Unsafe.As<ulong, float>(ref DataField);
        public readonly EventCategory CategoryValue => (EventCategory)(DataField >> 32);

        public ExtraValueUnion(long value)
        {
            DataField = (ulong)value;
        }

        public ExtraValueUnion(double value)
        {
            DataField = 0;
            Unsafe.As<ulong, double>(ref DataField) = value;
        }

        public ExtraValueUnion(float value)
        {
            DataField = 0;
            Unsafe.As<ulong, float>(ref DataField) = value;
        }

        public ExtraValueUnion(EventCategory category)
        {
            DataField = (ulong)category << 32;
        }
    }

    public struct ExtraData
    {
        public DataTypeOption Type;
        public ExtraValueUnion Value;
        public object? Object;
        public string? Format;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(object? obj, string? format = null)
        {
            Type = DataTypeOption.Object;
            Value = default;
            Object = obj;
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(EventCategory category, object? obj = null, string? format = null)
        {
            Type = DataTypeOption.ObjectAndCategory;
            Value = new(category);
            Object = obj;
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(long value, string? format = null)
        {
            Type = DataTypeOption.Long;
            Value = new(value);
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(double value, string? format = null)
        {
            Type = DataTypeOption.Double;
            Value = new(value);
            Format = format;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExtraData(float value, string? format = null)
        {
            Type = DataTypeOption.Float;
            Value = new(value);
            Format = format;
        }
    }

    [ProtoMember(1)] public int NameKey;
    [ProtoMember(2)] internal uint FlagsField;
    [ProtoMember(3)] public long StartTime;
    [ProtoMember(4)] public long EndTime;
    [ProtoMember(5)] public long MemoryBefore;
    [ProtoMember(6)] public long MemoryAfter;
    [ProtoMember(7)] public int Depth;
    [ProtoMember(8)] internal ulong DataValueField;
    /*            */ public object? DataObject; // Serialized as DataObjectKey via DataValueField
    [ProtoMember(9)] public string? DataFormat;

    public readonly string Name => ProfilerKeyCache.GetName(new(NameKey));

    public EventFlags Flags
    {
        readonly get => (EventFlags)(FlagsField & 0x00FFFFFF);
        set => FlagsField = (FlagsField & 0xFF000000) | (uint)value;
    }

    public DataTypeOption DataType
    {
        readonly get => (DataTypeOption)((FlagsField >> 24) & 0xFF);
        set => FlagsField = (FlagsField & 0x00FFFFFF) | ((uint)value << 24);
    }

    public ExtraValueUnion DataValue
    {
        readonly get => new ExtraValueUnion { DataField = DataValueField };
        set => DataValueField = value.DataField;
    }

    public readonly EventCategory Category => DataValue.CategoryValue;

    public ObjectId DataObjectKey
    {
        readonly get => new ObjectId((int)(uint)DataValueField);
        set => DataValueField = (DataValueField & ~0xFFFFFFFFUL) | (uint)value.ID;
    }

    public ExtraData ExtraValue
    {
        readonly get
        {
            ExtraData data;
            data.Type = DataType;
            data.Format = DataFormat;
            data.Object = DataObject;
            data.Value.DataField = DataValueField;

            return data;
        }
        set
        {
            DataType = value.Type;
            DataFormat = value.Format;
            DataObject = value.Object;
            DataValueField = value.Value.DataField;
        }
    }

    // TODO: Event chains for async task tracking
    // public int Next;

    public readonly bool MemoryTracked => (Flags & EventFlags.MemoryTracked) != 0;
    public readonly bool IsSinglePoint => (Flags & EventFlags.SinglePoint) != 0;

    public readonly long ElapsedTicks => EndTime - StartTime;
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
