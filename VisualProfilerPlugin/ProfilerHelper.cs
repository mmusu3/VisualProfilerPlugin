using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Havok;
using ProtoBuf;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Replication;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Network;
using VRageMath;
using VRageMath.Spatial;

namespace VisualProfiler;

static class ProfilerHelper
{
    internal static readonly IProfilerEventDataObjectResolver ProfilerEventObjectResolver = new ObjectResolver();

    class ObjectResolver : IProfilerEventDataObjectResolver
    {
        Dictionary<object, object> objectCache = [];

        public void Resolve(ref ProfilerEvent.ExtraData data) => ResolveProfilerEventObject(objectCache, ref data);

        public void ResolveNonCached(ref ProfilerEvent.ExtraData data) => ResolveProfilerEventObject(ref data);

        public void ClearCache() => objectCache.Clear();
    }

    static void ResolveProfilerEventObject(Dictionary<object, object> cache, ref ProfilerEvent.ExtraData data)
    {
        switch (data.Object)
        {
        case GCEventInfo:
            break;
        case Type type:
            {
                data.Format = "Type: {0}";

                if (cache.TryGetValue(type, out var cachedObj))
                {
                    data.Object = (string)cachedObj;
                    break;
                }

                cache[type] = data.Object = type.FullName!;
            }
            break;
        case Delegate @delegate:
            {
                data.Format = "Declaring Type: {0}";

                Type type;

                // TODO: Get more info from Target
                if (@delegate.Target != null)
                    type = @delegate.Target.GetType();
                else
                    type = @delegate.Method.DeclaringType!;

                if (cache.TryGetValue(type, out var cachedObj))
                    data.Object = (string)cachedObj;
                else
                    cache[type] = data.Object = type.FullName!;
            }
            break;
        case MyClusterTree.MyCluster cluster:
            {
                data.Format = "{0}";

                PhysicsClusterInfoProxy clusterInfo;

                if (cache.TryGetValue(cluster, out var cachedObj))
                {
                    clusterInfo = (PhysicsClusterInfoProxy)cachedObj;
                }
                else
                {
                    clusterInfo = new PhysicsClusterInfoProxy(cluster);
                    cache.Add(cluster, clusterInfo);
                }

                data.Object = clusterInfo.GetSnapshot(cluster);
            }
            break;
        case MyCubeGrid grid:
            {
                data.Format = "{0}";

                CubeGridInfoProxy gridInfo;

                if (cache.TryGetValue(grid, out var cachedObj))
                {
                    gridInfo = (CubeGridInfoProxy)cachedObj;
                }
                else
                {
                    gridInfo = new CubeGridInfoProxy(grid);
                    cache.Add(grid, gridInfo);
                }

                data.Object = gridInfo.GetSnapshot(grid);
            }
            break;
        case MyCubeBlock block:
            {
                data.Format = "{0}";

                CubeBlockInfoProxy blockInfo;

                if (cache.TryGetValue(block, out var cachedObj))
                {
                    blockInfo = (CubeBlockInfoProxy)cachedObj;
                }
                else
                {
                    blockInfo = new CubeBlockInfoProxy(block);
                    cache.Add(block, blockInfo);
                }

                var gridSnapshot = GetGridSnapshot(cache, block.CubeGrid);

                data.Object = blockInfo.GetSnapshot(gridSnapshot, block);
            }
            break;
        case MyExternalReplicable<MyCharacter> charRepl:
            {
                var character = charRepl.Instance;

                if (character == null)
                {
                    data.Object = null;
                    data.Format = "Empty character replicable{0}";
                    break;
                }

                data.Format = "{0}";

                CharacterInfoProxy charInfo;

                if (cache.TryGetValue(character, out var cachedObj))
                {
                    charInfo = (CharacterInfoProxy)cachedObj;
                }
                else
                {
                    charInfo = new CharacterInfoProxy(character);
                    cache.Add(character, charInfo);
                }

                data.Object = charInfo.GetSnapshot(character);
            }
            break;
        case MyExternalReplicable<MyCubeGrid> gridRepl:
            {
                var grid = gridRepl.Instance;

                if (grid == null)
                {
                    data.Object = null;
                    data.Format = "Empty cube grid replicable{0}";
                    break;
                }

                data.Format = "{0}";

                CubeGridInfoProxy gridInfo;

                if (cache.TryGetValue(grid, out var cachedObj))
                {
                    gridInfo = (CubeGridInfoProxy)cachedObj;
                }
                else
                {
                    gridInfo = new CubeGridInfoProxy(grid);
                    cache.Add(grid, gridInfo);
                }

                data.Object = gridInfo.GetSnapshot(grid);
            }
            break;
        case MyExternalReplicable<MySyncedBlock> blockRepl:
            {
                var block = blockRepl.Instance;

                if (block == null)
                {
                    data.Object = null;
                    data.Format = "Empty cube block replicable{0}";
                    break;
                }

                data.Format = "{0}";

                CubeBlockInfoProxy blockInfo;

                if (cache.TryGetValue(block, out var cachedObj))
                {
                    blockInfo = (CubeBlockInfoProxy)cachedObj;
                }
                else
                {
                    blockInfo = new CubeBlockInfoProxy(block);
                    cache.Add(block, blockInfo);
                }

                var gridSnapshot = GetGridSnapshot(cache, block.CubeGrid);

                data.Object = blockInfo.GetSnapshot(gridSnapshot, block);
            }
            break;
        case MyExternalReplicable<MyVoxelBase> voxelRepl:
            {
                data.Format = "{0}";

                var voxel = voxelRepl.Instance;

                if (voxel != null)
                {
                    if (cache.TryGetValue(voxel, out var cachedObj))
                    {
                        data.Object = (VoxelInfoProxy)cachedObj;
                        break;
                    }

                    cache[voxel] = data.Object = new VoxelInfoProxy(voxel);
                }
                else
                {
                    data.Object = null;
                    data.Object = "Empty voxel replicable{0}";
                }
            }
            break;
        case IMyReplicable replicable:
            {
                data.Object = null;
                data.Format = replicable.GetType().Name;
            }
            break;
        default:
            data.Object = GeneralStringCache.Intern(data.Object?.ToString());
            break;
        }
    }

    static CubeGridInfoProxy.Snapshot GetGridSnapshot(Dictionary<object, object> cache, MyCubeGrid grid)
    {
        CubeGridInfoProxy gridInfo;

        if (cache.TryGetValue(grid, out var cachedObj))
        {
            gridInfo = (CubeGridInfoProxy)cachedObj;
        }
        else
        {
            gridInfo = new CubeGridInfoProxy(grid);
            cache.Add(grid, gridInfo);
        }

        return gridInfo.GetSnapshot(grid);
    }

    static void ResolveProfilerEventObject(ref ProfilerEvent.ExtraData data)
    {
        switch (data.Object)
        {
        case GCEventInfo:
            break;
        case Type type:
            {
                data.Format = "Type: {0}";
                data.Object = type.FullName!;
            }
            break;
        case Delegate @delegate:
            {
                data.Format = "Declaring Type: {0}";

                Type type;

                // TODO: Get more info from Target
                if (@delegate.Target != null)
                    type = @delegate.Target.GetType();
                else
                    type = @delegate.Method.DeclaringType!;

                data.Object = type.FullName!;
            }
            break;
        default:
            data.Object = GeneralStringCache.Intern(data.Object?.ToString());
            break;
        }
    }

    internal static void PrepareRecordingForSerialization(ProfilerEventsRecording recording)
    {
        var objsToIds = new Dictionary<object, int>();

        foreach (var item in recording.Groups)
        {
            foreach (ref var _event in item.Value.AllEvents)
            {
                if (_event.ExtraValue.Type != ProfilerEvent.ExtraValueTypeOption.Object)
                    continue;

                var obj = _event.ExtraValue.Object;

                switch (obj)
                {
                case PhysicsClusterInfoProxy.Snapshot clusterSs:
                    GetObjId(clusterSs.Cluster);
                    break;
                case CubeGridInfoProxy.Snapshot gridSs:
                    GetObjId(gridSs.Grid);
                    break;
                case CubeBlockInfoProxy.Snapshot blockSs:
                    GetObjId(blockSs.Grid.Grid);
                    GetObjId(blockSs.Block);
                    break;
                case Type:
                    continue;
                }

                int id = GetObjId(obj);

                _event.ExtraValue.ObjectKey = new(id);
            }
        }

        int GetObjId(object? obj)
        {
            if (obj == null)
                return 0;

            if (obj is string str)
                return -GeneralStringCache.GetOrAdd(str).ID;

            if (!objsToIds.TryGetValue(obj, out int id))
            {
                id = 1 + objsToIds.Count;
                objsToIds.Add(obj, id);
                recording.DataObjects.Add(id, new(obj));
            }

            return id;
        }

        recording.DataStrings = GeneralStringCache.GetStrings();
    }

    internal static void RestoreRecordingObjectsAfterDeserialization(ProfilerEventsRecording recording)
    {
        GeneralStringCache.Clear();
        GeneralStringCache.Init(recording.DataStrings);

        foreach (var item in recording.Groups)
        {
            foreach (ref var _event in item.Value.AllEvents)
            {
                if (_event.NameKey != 0)
                    _event.NameKey = ProfilerKeyCache.GetOrAdd(recording.EventStrings[_event.NameKey]).GlobalIndex;

                if (_event.ExtraValue.Type != ProfilerEvent.ExtraValueTypeOption.Object)
                    continue;

                int objId = _event.ExtraValue.ObjectKey.ID;

                if (objId < 0)
                {
                    if (!recording.DataStrings.TryGetValue(-objId, out var str))
                    {
                        // Assert?
                    }

                    _event.ExtraValue.Object = str;
                }
                else
                {
                    if (!recording.DataObjects.TryGetValue(objId, out var obj))
                    {
                        // Assert?
                    }

                    _event.ExtraValue.Object = obj.Object;
                }
            }
        }
    }

    public static RecordingAnalysisInfo AnalyzeRecording(ProfilerEventsRecording recording)
    {
        var frameTimes = new List<long>();
        var clusters = new Dictionary<int, PhysicsClusterAnalysisInfo.Builder>();
        var grids = new Dictionary<long, CubeGridAnalysisInfo.Builder>();
        var progBlocks = new Dictionary<long, CubeBlockAnalysisInfo.Builder>();

        foreach (var group in recording.Groups)
        {
            int groupId = group.Key;
            var events = group.Value;

            if (events.FrameStartEventIndices.Length == 0
                || events.FrameEndEventIndices.Length == 0)
                continue;

            for (int f = 0; f < events.FrameStartEventIndices.Length; f++)
            {
                if (f >= events.FrameStartEventIndices.Length
                    || f >= events.FrameEndEventIndices.Length)
                    break;

                int startEventIndex = events.FrameStartEventIndices[f];
                int endEventIndex = events.FrameEndEventIndices[f];

                if (endEventIndex < startEventIndex)
                    continue;

                int startSegmentIndex = startEventIndex / events.SegmentSize;
                int endSegmentIndex = endEventIndex / events.SegmentSize;

                {
                    var firstSegment = events.EventSegments[startSegmentIndex].Events;
                    int startIndex = Math.Max(0, startEventIndex - startSegmentIndex * events.SegmentSize);

                    var lastSegment = events.EventSegments[endSegmentIndex].Events;
                    int endIndex = Math.Min(lastSegment.Length - 1, endEventIndex - endSegmentIndex * events.SegmentSize);

                    long startTime = firstSegment[startIndex].StartTime;
                    long endTime = lastSegment[endIndex].EndTime;
                    long frameTime = endTime - startTime;

                    if (frameTimes.Count <= f)
                        frameTimes.Add(0);

                    frameTimes[f] = Math.Max(frameTimes[f], frameTime);
                }

                for (int s = startSegmentIndex; s <= endSegmentIndex; s++)
                {
                    var segment = events.EventSegments[s].Events;
                    int startIndexInSegment = Math.Max(0, startEventIndex - s * events.SegmentSize);
                    int endIndexInSegment = Math.Min(segment.Length - 1, endEventIndex - s * events.SegmentSize);

                    for (int e = startIndexInSegment; e <= endIndexInSegment; e++)
                    {
                        ref var _event = ref segment[e];

                        switch (_event.ExtraValue.Type)
                        {
                        case ProfilerEvent.ExtraValueTypeOption.Object:
                            {
                                // TODO: Record list of event IDs per object for highlighting events in graph when object selected

                                switch (_event.ExtraValue.Object)
                                {
                                case PhysicsClusterInfoProxy.Snapshot clusterInfo:
                                    AnalyzePhysicsCluster(clusterInfo, in _event, groupId, f);
                                    break;
                                case CubeGridInfoProxy.Snapshot gridInfo:
                                    AnalyzeGrid(gridInfo, in _event, groupId, f);
                                    break;
                                case CubeBlockInfoProxy.Snapshot blockInfo:
                                    AnalyzeBlock(blockInfo, in _event, groupId, f);
                                    break;
                                }
                            }
                            break;
                        case ProfilerEvent.ExtraValueTypeOption.Long:
                        case ProfilerEvent.ExtraValueTypeOption.Double:
                        case ProfilerEvent.ExtraValueTypeOption.Float:
                            break;
                        }
                    }
                }
            }

        }

        RecordingAnalysisInfo.FrameTimeInfo frameTimeInfo = default;
        frameTimeInfo.Min = double.PositiveInfinity;

        foreach (long frameTime in frameTimes)
        {
            double frameMilliseconds = ProfilerTimer.MillisecondsFromTicks(frameTime);

            frameTimeInfo.Min = Math.Min(frameTimeInfo.Min, frameMilliseconds);
            frameTimeInfo.Max = Math.Max(frameTimeInfo.Max, frameMilliseconds);
            frameTimeInfo.Mean += frameMilliseconds;
            frameTimeInfo.StdDev += frameMilliseconds * frameMilliseconds;
        }

        frameTimeInfo.Mean /= frameTimes.Count;
        frameTimeInfo.StdDev = Math.Sqrt(frameTimeInfo.StdDev / frameTimes.Count - frameTimeInfo.Mean * frameTimeInfo.Mean);

        foreach (var item in clusters)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        foreach (var item in grids)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        foreach (var item in progBlocks)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        return new RecordingAnalysisInfo(frameTimeInfo,
            clusters.Values.Select(c => c.Finish()).ToArray(),
            grids.Values.Select(c => c.Finish()).ToArray(),
            progBlocks.Values.Select(c => c.Finish()).ToArray());

        void AnalyzePhysicsCluster(PhysicsClusterInfoProxy.Snapshot clusterInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (clusters.TryGetValue(clusterInfo.Cluster.ID, out var anInf))
                anInf.Add(clusterInfo);
            else
                clusters.Add(clusterInfo.Cluster.ID, anInf = new(clusterInfo));

            // TODO: Filter parent events to prevent time overlap
            switch (_event.Name)
            {
            default:
                break;
            }

            anInf.TotalTime += _event.ElapsedMilliseconds;
            anInf.IncludedInGroups.Add(groupId);
            anInf.FramesCounted.Add(frameIndex);
        }

        void AnalyzeGrid(CubeGridInfoProxy.Snapshot gridInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (grids.TryGetValue(gridInfo.Grid.EntityId, out var anInf))
                anInf.Add(gridInfo);
            else
                grids.Add(gridInfo.Grid.EntityId, anInf = new(gridInfo));

            // TODO: Filter parent events to prevent time overlap
            switch (_event.Name)
            {
            default:
                break;
            }

            anInf.TotalTime += _event.ElapsedMilliseconds;
            anInf.IncludedInGroups.Add(groupId);
            anInf.FramesCounted.Add(frameIndex);
        }

        void AnalyzeBlock(CubeBlockInfoProxy.Snapshot blockInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (blockInfo.Block.BlockType.Type != typeof(Sandbox.Game.Entities.Blocks.MyProgrammableBlock))
                return;

            if (progBlocks.TryGetValue(blockInfo.Block.EntityId, out var anInf))
                anInf.Add(blockInfo);
            else
                progBlocks.Add(blockInfo.Block.EntityId, anInf = new(blockInfo));

            // TODO: Filter parent events to prevent time overlap
            switch (_event.Name)
            {
            default:
                break;
            }

            anInf.TotalTime += _event.ElapsedMilliseconds;
            anInf.IncludedInGroups.Add(groupId);
            anInf.FramesCounted.Add(frameIndex);
        }
    }

    class AccumTimer(ProfilerKey nameKey)
    {
        public ProfilerKey NameKey = nameKey;
        public ProfilerEvent.EventCategory Category;

        public long ElapsedTime;
        public long MinElapsedTime = long.MaxValue;
        public long MaxElapsedTime;
        public double ElapsedTimeM;
        public double ElapsedTimeS;

        public long AllocatedMemory;
        public bool MemoryTracked;
        public int AccumCount;
        public AccumTimer? Parent;
        public Dictionary<int, AccumTimer> Children = [];

        public override string ToString() => $"{NameKey}, Children: {Children.Count}";
    }

    internal static (long Time, (int GroupId, ProfilerEventsSegment Group)[] Groups) CombineFrames(ProfilerEventsRecording recording)
    {
        var combinedGroups = new (int, ProfilerEventsSegment)[recording.Groups.Count];
        var combinedEvents = new List<ProfilerEvent>();
        var timers = new Dictionary<int, AccumTimer>();

        AccumTimer? activeTimer;

        AccumTimer GetOrAddTimer(int key, int depthDir)
        {
            if (depthDir < 0)
            {
                activeTimer = activeTimer!.Parent;

                for (int i = 0; i < -depthDir; i++)
                    activeTimer = activeTimer?.Parent;
            }
            else if (depthDir == 0)
            {
                activeTimer = activeTimer?.Parent;
            }

            if (activeTimer != null)
            {
                if (!activeTimer.Children.TryGetValue(key, out var subTimer))
                {
                    subTimer = new(new(key)) { Parent = activeTimer };
                    activeTimer.Children.Add(key, subTimer);
                }

                return subTimer;
            }
            else
            {
                if (!timers.TryGetValue(key, out var timer))
                {
                    timer = new(new(key));
                    timers.Add(key, timer);
                }

                return timer;
            }
        }

        long maxTime = long.MinValue;
        int i = 0;

        foreach (var (groupId, group) in recording.Groups)
        {
            activeTimer = null;

            int prevDepth = 0;

            foreach (ref var _event in group.AllEvents)
            {
                if (_event.IsSinglePoint)
                    continue;

                long elapsedTime = _event.EndTime - _event.StartTime;
                long allocdMem = _event.MemoryAfter - _event.MemoryBefore;

                activeTimer = GetOrAddTimer(_event.NameKey, _event.Depth - prevDepth);

                var category = GetCategory(ref _event);

                if (category != ProfilerEvent.EventCategory.Other)
                    activeTimer.Category = category;

                activeTimer.AccumCount++;
                activeTimer.ElapsedTime += elapsedTime;
                activeTimer.MinElapsedTime = Math.Min(activeTimer.MinElapsedTime, elapsedTime);
                activeTimer.MaxElapsedTime = Math.Max(activeTimer.MaxElapsedTime, elapsedTime);

                if (activeTimer.AccumCount == 1)
                {
                    activeTimer.ElapsedTimeM = ProfilerTimer.MillisecondsFromTicks(elapsedTime);
                    activeTimer.ElapsedTimeS = 0;
                }
                else
                {
                    double x = ProfilerTimer.MillisecondsFromTicks(elapsedTime);
                    double m = activeTimer.ElapsedTimeM;
                    double s = activeTimer.ElapsedTimeS;

                    activeTimer.ElapsedTimeM = m + (x - m) / activeTimer.AccumCount;
                    activeTimer.ElapsedTimeS = s + (x - m) * (x - activeTimer.ElapsedTimeM);
                }

                activeTimer.AllocatedMemory += allocdMem;
                activeTimer.MemoryTracked = _event.MemoryTracked;

                prevDepth = _event.Depth;
            }

            long groupTime = 0;

            foreach (var item in timers.Values)
            {
                DescendTimer(item, groupTime, 0);

                if (item.Category != ProfilerEvent.EventCategory.Wait)
                    groupTime += item.ElapsedTime;
            }

            timers.Clear();

            void DescendTimer(AccumTimer timer, long startTime, int depth)
            {
                if (timer.Category == ProfilerEvent.EventCategory.Wait)
                    return;

                combinedEvents.Add(new ProfilerEvent {
                    NameKey = timer.NameKey.GlobalIndex,
                    Depth = depth,
                    StartTime = startTime,
                    EndTime = startTime + timer.ElapsedTime,
                    MemoryAfter = timer.AllocatedMemory,
                    Flags = timer.MemoryTracked ? ProfilerEvent.EventFlags.MemoryTracked : ProfilerEvent.EventFlags.None,
                    ExtraValue = new(timer.Category, new CombinedEventInfo(timer), "{0}")
                });

                long s = startTime;

                foreach (var item in timer.Children.Values)
                {
                    DescendTimer(item, s, depth + 1);
                    s += item.ElapsedTime;
                }
            }

            if (groupTime > maxTime)
                maxTime = groupTime;

            combinedGroups[i++] = (groupId, new ProfilerEventsSegment { EndTime = groupTime, Events = combinedEvents.ToArray() });
            combinedEvents.Clear();
        }

        return (maxTime, combinedGroups);

        static ProfilerEvent.EventCategory GetCategory(ref ProfilerEvent _event)
        {
            if (_event.ExtraValue.Type == ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory)
                return _event.ExtraValue.Value.CategoryValue;

            if (_event.ExtraValue.Type == ProfilerEvent.ExtraValueTypeOption.Object)
            {
                switch (_event.ExtraValue.Object)
                {
                case CubeGridInfoProxy.Snapshot:
                    return ProfilerEvent.EventCategory.Grids;
                case CubeBlockInfoProxy.Snapshot:
                    return ProfilerEvent.EventCategory.Blocks;
                case CharacterInfoProxy.Snapshot:
                    return ProfilerEvent.EventCategory.Characters;
                }
            }

            return ProfilerEvent.EventCategory.Other;
        }
    }

    public static string SummarizeRecording(ProfilerEventsRecording recording)
    {
        var (_, groups) = CombineFrames(recording);

        var header = $"Recorded {recording.NumFrames} frames over {recording.ElapsedTime.TotalSeconds} seconds.";

        if (groups.Length == 0)
            return header;

        var mainGroup = groups[0].Group;
        var times = new (ProfilerEvent.EventCategory Category, double AvgTime, double TotalTime)[(int)ProfilerEvent.EventCategory.CategoryCount];

        for (int i = 0; i < times.Length; i++)
            times[i].Category = (ProfilerEvent.EventCategory)i;

        for (int i = 0; i < mainGroup.Events.Length; i++)
        {
            ref var _event = ref mainGroup.Events[i];
            ref var t = ref times[(int)_event.ExtraValue.Value.CategoryValue];

            t.TotalTime += _event.ElapsedTime.TotalMilliseconds;
            t.AvgTime += ((CombinedEventInfo)_event.ExtraValue.Object!).MillisecondsAverage;
        }

        Array.Sort(times, (a, b) => b.TotalTime.CompareTo(a.TotalTime));

        var sb = new StringBuilder(header);
        sb.AppendLine().AppendLine("Times Summary (ms Avg/Frame - Total):");

        for (int i = 0; i < times.Length; i++)
        {
            var t = times[i];

            if (t.TotalTime == 0)
                break;

            if (t.Category is ProfilerEvent.EventCategory.Wait or ProfilerEvent.EventCategory.Other)
                continue;

            var cat = t.Category.ToString();

            sb.Append(cat).Append(':').Append(' ', 10 - cat.Length).Append("  ")
                .AppendFormat("{0:N2}  -  ", t.AvgTime)
                .AppendFormat("{0:N1}", t.TotalTime);

            if (i < times.Length - 1 && (i > times.Length - 2 || times[i + 1].TotalTime != 0))
                sb.AppendLine();
        }

        return sb.ToString();
    }

    class CombinedEventInfo
    {
        public int CallCount;
        public double MillisecondsMin;
        public double MillisecondsMax;
        public double MillisecondsAverage;
        public double MillisecondsVariance;

        public CombinedEventInfo(AccumTimer timer)
        {
            CallCount = timer.AccumCount;
            MillisecondsMin = ProfilerTimer.MillisecondsFromTicks(timer.MinElapsedTime);
            MillisecondsMax = ProfilerTimer.MillisecondsFromTicks(timer.MaxElapsedTime);
            MillisecondsAverage = timer.ElapsedTimeM;
            MillisecondsVariance = timer.AccumCount > 1 ? timer.ElapsedTimeS / (timer.AccumCount - 1) : 0;
        }

        public override string ToString()
        {
            return $"""
                    Call Count: {CallCount}
                    Min ms: {MillisecondsMin:N3}
                    Max ms: {MillisecondsMax:N3}
                    Average ms: {MillisecondsAverage:N2}
                    StdDev ms: {Math.Sqrt(MillisecondsVariance):N2}
                    """;
        }
    }
}

[ProtoContract]
struct StringId(int id)
{
    [ProtoMember(1)] public int ID = id;
}

static class GeneralStringCache
{
    static readonly Dictionary<string, int> stringsToIds = [];
    static readonly Dictionary<int, string> idsToStrings = [];
    static int idGenerator = 1;

    public static void Init(Dictionary<int, string> values)
    {
        int max = 0;

        foreach (var (key, value) in values)
        {
            idsToStrings.Add(key, value);
            stringsToIds.Add(value, key);

            if (key > max)
                max = key;
        }

        idGenerator = max + 1;
    }

    public static StringId GetOrAdd(string? value)
    {
        if (value == null)
            return default;

        int id;

        if (!stringsToIds.TryGetValue(value, out id))
        {
            id = idGenerator++;
            stringsToIds.Add(value, id);
            idsToStrings.Add(id, value);
        }

        return new StringId(id);
    }

    public static bool TryGet(string value, out StringId id)
    {
        int index;

        if (!stringsToIds.TryGetValue(value, out index))
        {
            id = default;
            return false;
        }

        id = new StringId(index);
        return true;
    }

    public static string? Get(StringId id)
    {
        if (id.ID == 0)
            return null;

        return idsToStrings[id.ID];
    }

    public static string? Intern(string? value)
    {
        if (value == null)
            return null;

        if (stringsToIds.TryGetValue(value, out int id))
            return idsToStrings[id];

        id = idGenerator++;
        stringsToIds.Add(value, id);
        idsToStrings.Add(id, value);

        return value;
    }

    public static Dictionary<int, string> GetStrings() => new(idsToStrings);

    public static void Clear()
    {
        stringsToIds.Clear();
        idsToStrings.Clear();
        idGenerator = 1;
    }
}

[ProtoContract]
struct TypeProxy
{
    [ProtoIgnore]
    public Type Type => type ??= Type.GetType(GeneralStringCache.Get(TypeName)!)!;
    Type type;

    [ProtoMember(1)]
    public StringId TypeName;

    public TypeProxy(Type type)
    {
        this.type = type;
        TypeName = GeneralStringCache.GetOrAdd(Type?.AssemblyQualifiedName ?? "");
    }

    public TypeProxy() => type = null!;

    public override string ToString() => Type?.ToString()!;
}

[ProtoContract]
public struct ObjectId(int id)
{
    [ProtoMember(1)] public int ID = id;
}

[ProtoContract]
public struct RefObjWrapper(object obj)
{
    [ProtoMember(1, DynamicType = true, AsReference = true)] public object Object = obj;
}

[ProtoContract]
public struct RefObjWrapper<T>(T obj) where T : class
{
    [ProtoMember(1, AsReference = true)] public T Object = obj;
}

[ProtoContract]
class PhysicsClusterInfoProxy
{
    [ProtoMember(1)] public int ID;
    [ProtoMember(2)] public List<RefObjWrapper<Snapshot>> Snapshots = [];

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public PhysicsClusterInfoProxy Cluster;
        [ProtoMember(2)] public BoundingBoxD AABB;
        [ProtoMember(3)] public bool HasWorld;
        [ProtoMember(4)] public int RigidBodyCount;
        [ProtoMember(5)] public int ActiveRigidBodyCount;
        [ProtoMember(6)] public int CharacterCount;

        public Snapshot(PhysicsClusterInfoProxy clusterInfo, MyClusterTree.MyCluster cluster)
        {
            Cluster = clusterInfo;
            AABB = cluster.AABB;

            if (cluster.UserData is HkWorld hkWorld)
            {
                HasWorld = true;
                RigidBodyCount = hkWorld.RigidBodies.Count;
                ActiveRigidBodyCount = hkWorld.ActiveRigidBodies.Count;
                CharacterCount = hkWorld.CharacterRigidBodies.Count;
            }
        }

        public Snapshot()
        {
            Cluster = null!;
        }

        public bool Equals(MyClusterTree.MyCluster cluster)
        {
            if (AABB != cluster.AABB)
                return false;

            if (cluster.UserData is HkWorld hkWorld)
            {
                if (!HasWorld)
                    return false;

                return RigidBodyCount == hkWorld.RigidBodies.Count
                    && ActiveRigidBodyCount == hkWorld.ActiveRigidBodies.Count
                    && CharacterCount == hkWorld.CharacterRigidBodies.Count;
            }
            else
            {
                return !HasWorld;
            }
        }

        public override string ToString()
        {
            if (!HasWorld)
                return "Physics Cluster without HKWorld";

            return $"""
                Physics Cluster, ID: {Cluster.ID}
                   Center: {Vector3D.Round(AABB.Center, 0)}
                   Size: {Vector3D.Round(AABB.Size, 0)}
                   Rigid Bodies: {RigidBodyCount} (Active: {ActiveRigidBodyCount})
                   Characters: {CharacterCount}
                """;
        }
    }

    public PhysicsClusterInfoProxy(MyClusterTree.MyCluster cluster)
    {
        ID = cluster.ClusterId;
    }

    public PhysicsClusterInfoProxy() { }

    public Snapshot GetSnapshot(MyClusterTree.MyCluster cluster)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1].Object : null;

        if (lastSnapshot != null && lastSnapshot.Equals(cluster))
            return lastSnapshot;

        var snapshot = new Snapshot(this, cluster);

        Snapshots.Add(new(snapshot));

        return snapshot;
    }
}

[ProtoContract]
class CubeGridInfoProxy
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public MyCubeSize GridSize;
    // Must preserve field member IDs
    [ProtoMember(4)] public bool IsNPC;
    [ProtoMember(3)] public List<RefObjWrapper<Snapshot>> Snapshots = [];

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public CubeGridInfoProxy Grid;
        [ProtoMember(2)] public ulong FrameIndex;
        // Must preserve field member IDs
        [ProtoMember(8)] public bool IsStatic;
        [ProtoMember(3)] public string CustomName;
        [ProtoMember(4)] public long OwnerId;

        [ProtoMember(5)]
        public StringId OwnerNameId
        {
            get => ownerNameId;
            set => ownerNameId = value;
        }
        StringId ownerNameId;

        [ProtoIgnore]
        public string? OwnerName
        {
            get
            {
                if (ownerName == null && ownerNameId.ID > 0)
                    ownerName = GeneralStringCache.Get(ownerNameId);

                return ownerName;
            }
            set
            {
                ownerName = value;
                ownerNameId = GeneralStringCache.GetOrAdd(value);
            }
        }
        string? ownerName;

        [ProtoMember(6)] public int BlockCount;
        [ProtoMember(7)] public Vector3D Position;
        // Must preserve field member IDs
        [ProtoMember(9)] public float Speed;
        [ProtoMember(10)] public Vector3I Size;
        [ProtoMember(11)] public int PCU;
        [ProtoMember(12)] public bool IsPowered;

        public Snapshot(CubeGridInfoProxy gridInfo, MyCubeGrid grid)
        {
            Grid = gridInfo;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;
            IsStatic = grid.IsStatic;

            long ownerId = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            CustomName = grid.DisplayName;
            OwnerId = ownerId;
            OwnerName = ownerIdentity?.DisplayName;
            BlockCount = grid.BlocksCount;
            PCU = grid.BlocksPCU;
            Size = grid.Max - grid.Min;
            Position = grid.PositionComp.GetPosition();
            Speed = grid.LinearVelocity.Length();
            IsPowered = grid.IsPowered;
        }

        public Snapshot()
        {
            Grid = null!;
            CustomName = "";
        }

        public bool Equals(MyCubeGrid grid)
        {
            long ownerId = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            return IsStatic == grid.IsStatic
                && CustomName == grid.DisplayName
                && OwnerId == ownerId
                && OwnerName == ownerIdentity?.DisplayName
                && BlockCount == grid.BlocksCount
                && PCU == grid.BlocksPCU
                && Size == grid.Max - grid.Min
                && Vector3D.Round(Position, 1) == Vector3D.Round(grid.PositionComp.GetPosition(), 1)
                && Math.Round(Speed, 1) == Math.Round(grid.LinearVelocity.Length(), 1)
                && IsPowered == grid.IsPowered;
        }

        public override string ToString()
        {
            var idPart = OwnerName != null ? $", ID: " : null;

            return $"""
                {(IsStatic && Grid.GridSize == MyCubeSize.Large ? "Static" : Grid.GridSize)} Grid, ID: {Grid.EntityId}{(Grid.IsNPC ? "\n    IsNPC: true" : "")}
                   Custom Name: {CustomName}
                   Owner: {OwnerName}{idPart}{OwnerId}
                   Blocks: {BlockCount}
                   PCU: {PCU}
                   Size: {Size}
                   Position: {Vector3D.Round(Position, 0)}
                   Speed: {Math.Round(Speed, 1)}
                   IsPowered: {IsPowered}
                """;
        }
    }

    public CubeGridInfoProxy(MyCubeGrid grid)
    {
        EntityId = grid.EntityId;
        GridSize = grid.GridSizeEnum;
        IsNPC = grid.IsNpcSpawnedGrid;
    }

    public CubeGridInfoProxy()
    {
    }

    public Snapshot GetSnapshot(MyCubeGrid grid)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1].Object : null;

        if (lastSnapshot != null && lastSnapshot.Equals(grid))
            return lastSnapshot;

        var snapshot = new Snapshot(this, grid);

        Snapshots.Add(new(snapshot));

        return snapshot;
    }
}

[ProtoContract]
class CubeBlockInfoProxy
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public TypeProxy BlockType;
    [ProtoMember(3)] public List<RefObjWrapper<Snapshot>> Snapshots = [];

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public CubeGridInfoProxy.Snapshot Grid;
        [ProtoMember(2, AsReference = true)] public CubeBlockInfoProxy Block;
        [ProtoMember(3)] public ulong FrameIndex;
        [ProtoMember(4)] public string? CustomName;
        [ProtoMember(5)] public long OwnerId;

        [ProtoMember(6)]
        public StringId OwnerNameId
        {
            get => ownerNameId;
            set => ownerNameId = value;
        }
        StringId ownerNameId;

        [ProtoIgnore]
        public string? OwnerName
        {
            get
            {
                if (ownerName == null && ownerNameId.ID > 0)
                    ownerName = GeneralStringCache.Get(ownerNameId);

                return ownerName;
            }
            set
            {
                ownerName = value;
                ownerNameId = GeneralStringCache.GetOrAdd(value);
            }
        }
        string? ownerName;

        [ProtoMember(7)] public Vector3D Position;

        public Snapshot(CubeGridInfoProxy.Snapshot gridInfo, CubeBlockInfoProxy blockInfo, MyCubeBlock block)
        {
            Grid = gridInfo;
            Block = blockInfo;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;

            long ownerId = block.OwnerId;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            CustomName = (block as MyTerminalBlock)?.CustomName.ToString();
            OwnerId = ownerId;
            OwnerName = ownerIdentity?.DisplayName;
            Position = block.PositionComp.GetPosition();
        }

        public Snapshot()
        {
            Grid = null!;
            Block = null!;
        }

        public bool Equals(MyCubeBlock block)
        {
            long ownerId = block.OwnerId;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            return CustomName == (block as MyTerminalBlock)?.CustomName.ToString()
                && OwnerId == ownerId
                && OwnerName == ownerIdentity?.DisplayName
                && Vector3D.Round(Position, 1) == Vector3D.Round(block.PositionComp.GetPosition(), 1);
        }

        public override string ToString()
        {
            var idPart = OwnerName != null ? $", ID: " : null;

            return $"""
                {Block.BlockType.Type.Name}, ID: {Block.EntityId}
                   Custom Name: {CustomName}
                   Owner: {OwnerName}{idPart}{OwnerId}
                   Position: {Vector3D.Round(Position, 1)}
                {Grid}
                """;
        }
    }

    public CubeBlockInfoProxy(MyCubeBlock block)
    {
        EntityId = block.EntityId;
        BlockType = new TypeProxy(block.GetType());
    }

    public CubeBlockInfoProxy()
    {
    }

    public Snapshot GetSnapshot(CubeGridInfoProxy.Snapshot gridInfo, MyCubeBlock block)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1].Object : null;

        if (lastSnapshot != null && lastSnapshot.Equals(block))
            return lastSnapshot;

        var snapshot = new Snapshot(gridInfo, this, block);

        Snapshots.Add(new(snapshot));

        return snapshot;
    }
}

[ProtoContract]
class CharacterInfoProxy
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public long IdentityId;
    [ProtoMember(3)] public ulong PlatformId;
    [ProtoMember(4)] public string Name;
    [ProtoMember(5)] public List<RefObjWrapper<Snapshot>> Snapshots = [];

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public CharacterInfoProxy Character;
        [ProtoMember(2)] public Vector3D Position;

        public Snapshot(CharacterInfoProxy characterInfo, MyCharacter character)
        {
            Character = characterInfo;
            Position = character.PositionComp.GetPosition();
        }

        public Snapshot()
        {
            Character = null!;
        }

        public override string ToString()
        {
            return $"""
                Character, ID: {Character.EntityId}
                   Identity ID: {Character.IdentityId}
                   Platform ID: {Character.PlatformId}
                   Name: {Character.Name}
                   Position: {Vector3D.Round(Position, 1)}
                """;
        }
    }

    public CharacterInfoProxy(MyCharacter character)
    {
        EntityId = character.EntityId;

        var identity = character.GetIdentity();
        IdentityId = identity?.IdentityId ?? 0;
        PlatformId = character.ControlSteamId;
        Name = identity?.DisplayName ?? "";
    }

    public CharacterInfoProxy()
    {
        Name = "";
    }

    public Snapshot GetSnapshot(MyCharacter character)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1].Object : null;

        if (lastSnapshot != null && lastSnapshot.Equals(character))
            return lastSnapshot;

        var snapshot = new Snapshot(this, character);

        Snapshots.Add(new(snapshot));

        return snapshot;
    }
}

[ProtoContract]
class VoxelInfoProxy
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public string Name;
    [ProtoMember(3)] public BoundingBoxD AABB;

    public VoxelInfoProxy(MyVoxelBase voxel)
    {
        EntityId = voxel.EntityId;
        Name = voxel.Name;
        AABB = voxel.PositionComp.WorldAABB;
    }

    public VoxelInfoProxy()
    {
        Name = "";
    }

    public override string ToString()
    {
        return $"""
                Voxel, ID: {EntityId}
                   Name: {Name}
                   Center: {Vector3D.Round(AABB.Center, 0)}
                   Size: {Vector3D.Round(AABB.Size, 0)}
                """;
    }
}

class RecordingAnalysisInfo
{
    public struct FrameTimeInfo
    {
        public double Min, Max, Mean, StdDev;
    }

    public FrameTimeInfo FrameTimes;

    public PhysicsClusterAnalysisInfo[] PhysicsClusters;
    public CubeGridAnalysisInfo[] Grids;
    public CubeBlockAnalysisInfo[] ProgrammableBlocks;

    internal RecordingAnalysisInfo(FrameTimeInfo frameTimes, PhysicsClusterAnalysisInfo[] physicsClusters,
        CubeGridAnalysisInfo[] grids, CubeBlockAnalysisInfo[] programmableBlocks)
    {
        FrameTimes = frameTimes;
        PhysicsClusters = physicsClusters;
        Grids = grids;
        ProgrammableBlocks = programmableBlocks;
    }
}

class PhysicsClusterAnalysisInfo
{
    public class Builder
    {
        public int ID;
        public HashSet<BoundingBoxD> AABBs = [];
        public HashSet<int> NumObjects = [];
        public HashSet<int> NumActiveObjects = [];
        public HashSet<int> NumCharacters = [];

        public double TotalTime;
        public double AverageTimePerFrame;
        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public Builder(PhysicsClusterInfoProxy.Snapshot info)
        {
            ID = info.Cluster.ID;

            Add(info);
        }

        public void Add(PhysicsClusterInfoProxy.Snapshot info)
        {
            AABBs.Add(info.AABB);

            if (info.HasWorld)
            {
                NumObjects.Add(info.RigidBodyCount);
                NumActiveObjects.Add(info.ActiveRigidBodyCount);
                NumCharacters.Add(info.CharacterCount);
            }
        }

        public PhysicsClusterAnalysisInfo Finish()
        {
            return new PhysicsClusterAnalysisInfo(ID, AABBs.ToArray(),
                NumObjects.ToArray(), NumActiveObjects.ToArray(), NumCharacters.ToArray(),
                TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public int ID { get; set; }
    public BoundingBoxD[] AABBs;
    public int[] NumObjects;
    public int[] NumActiveObjects;
    public int[] NumCharacters;

    public double TotalTime { get; set; }
    public double AverageTimePerFrame { get; set; }
    public int IncludedInNumGroups { get; set; }
    public int NumFramesCounted { get; set; }

    public Vector3D AveragePosition
    {
        get
        {
            var avgPos = AABBs[0].Center;

            for (int i = 1; i < AABBs.Length; i++)
                avgPos += AABBs[i].Center;

            return avgPos / AABBs.Length;
        }
    }

    static string GetMinMaxCountString(int[] counts)
    {
        int minCount = int.MaxValue;
        int maxCount = int.MinValue;

        for (int i = 0; i < counts.Length; i++)
        {
            int c = counts[i];

            if (c < minCount)
                minCount = c;

            if (c > maxCount)
                maxCount = c;
        }

        return minCount == maxCount ? minCount.ToString() : $"{minCount} - {maxCount}";
    }

    public string ObjectCountsForColumn => GetMinMaxCountString(NumObjects);
    public string ActiveObjectCountsForColumn => GetMinMaxCountString(NumActiveObjects);
    public string CharacterCountsForColumn => GetMinMaxCountString(NumCharacters);

    public string SizeForColumn
    {
        get
        {
            double minSize = double.MaxValue;
            double maxSize = double.MinValue;
            int minIndex = -1;
            int maxIndex = -1;

            for (int i = 0; i < AABBs.Length; i++)
            {
                var box = AABBs[i];
                double size = box.Volume;

                if (size < minSize)
                {
                    minSize = size;
                    minIndex = i;
                }

                if (size > maxSize)
                {
                    maxSize = size;
                    maxIndex = i;
                }
            }

            var minExt = Vector3D.Round(AABBs[minIndex].Size, 0);
            var maxExt = Vector3D.Round(AABBs[maxIndex].Size, 0);

            return minExt == maxExt ? minExt.ToString() : $"{minExt} - {maxExt}";
        }
    }

    public string AveragePositionForColumn => Vector3D.Round(AveragePosition, 0).ToString();

    public PhysicsClusterAnalysisInfo(
        int id, BoundingBoxD[] aabbs,
        int[] numObjects, int[] numActiveObjects, int[] numCharacters,
        double totalTime, double averageTimePerFrame,
        int includedInNumGroups, int numFramesCounted)
    {
        ID = id;
        AABBs = aabbs;
        NumObjects = numObjects;
        NumActiveObjects = numActiveObjects;
        NumCharacters = numCharacters;
        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumGroups = includedInNumGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        int minObjects = NumObjects.Min();
        int maxObjects = NumObjects.Max();

        int minActiveObjects = NumActiveObjects.Min();
        int maxActiveObjects = NumActiveObjects.Max();

        int minCharacters = NumCharacters.Min();
        int maxCharacters = NumCharacters.Max();

        return $"""
                Physics Cluster, ID: {ID}
                    {(AABBs.Length == 1
                        ? ToString(Round(AABBs[0]))
                        : $"AABBs: {string.Join(", ", AABBs.Select(Round).Distinct().Select(ToString2))}")}
                    Num Objects{(minObjects == maxObjects ? $": {maxObjects}" : $", Min: {minObjects}, Max: {maxObjects}")}
                    Num Active Objects{(minActiveObjects == maxActiveObjects ? $": {maxActiveObjects}" : $", Min: {minActiveObjects}, Max: {maxActiveObjects}")}
                    Num Characters{(minCharacters == maxCharacters ? $": {maxCharacters}" : $", Min: {minCharacters}, Max: {maxCharacters}")}
                Total Time: {TotalTime:N1}ms
                Average Time: {AverageTimePerFrame:N2}ms
                Counted Frames: {NumFramesCounted}{(IncludedInNumGroups > 1 ? $"\r\nProcessed over {IncludedInNumGroups} threads" : "")}
                """;

        static BoundingBoxD Round(BoundingBoxD box) => new BoundingBoxD(Vector3D.Round(box.Min, 0), Vector3D.Round(box.Max, 0));

        static string ToString(BoundingBoxD box)
        {
            return $$"""
                    Center:{{{box.Center}}}
                        Size:{{{box.Size}}}
                    """;
        }

        static string ToString2(BoundingBoxD box)
        {
            return $"(Center:{{{box.Center}}}, Size:{{{box.Size}}})";
        }
    }
}

class CubeGridAnalysisInfo
{
    public class Builder
    {
        public long EntityId;
        public MyCubeSize GridSize;
        public bool IsNPC;
        public bool? IsStatic; // Null value means mixed
        public HashSet<string> CustomNames = [];
        public Dictionary<long, string?> Owners = [];
        public HashSet<int> BlockCounts = [];
        public HashSet<int> PCUs = [];
        public HashSet<Vector3I> Sizes = [];
        public HashSet<Vector3D> Positions = [];
        public HashSet<float> Speeds = [];
        public bool? IsPowered; // Null value means mixed
        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime;
        public double AverageTimePerFrame;

        public Builder(CubeGridInfoProxy.Snapshot info)
        {
            EntityId = info.Grid.EntityId;
            GridSize = info.Grid.GridSize;
            IsStatic = info.IsStatic;
            IsNPC = info.Grid.IsNPC;
            IsPowered = info.IsPowered;

            Add(info);
        }

        public void Add(CubeGridInfoProxy.Snapshot info)
        {
            if (IsStatic != null && info.IsStatic != IsStatic)
                IsStatic = null;

            CustomNames.Add(info.CustomName);
            Owners[info.OwnerId] = info.OwnerName;
            BlockCounts.Add(info.BlockCount);
            PCUs.Add(info.PCU);
            Sizes.Add(info.Size);
            Positions.Add(info.Position);
            Speeds.Add(info.Speed);

            if (IsPowered != null && info.IsPowered != IsPowered)
                IsPowered = null;
        }

        public CubeGridAnalysisInfo Finish()
        {
            return new CubeGridAnalysisInfo(EntityId, GridSize, IsStatic, IsNPC, CustomNames.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                BlockCounts.ToArray(), PCUs.ToArray(), Sizes.ToArray(), Positions.ToArray(), Speeds.ToArray(), IsPowered,
                TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public long EntityId { get; set; }
    public MyCubeSize GridSize;
    public bool? IsStatic;
    public bool IsNPC;
    public string[] CustomNames;
    public (long ID, string? Name)[] Owners;
    public int[] BlockCounts;
    public int[] PCUs;
    public Vector3I[] Sizes;
    public Vector3D[] Positions;
    public float[] Speeds;
    public bool? IsPowered;

    public double TotalTime { get; set; }
    public double AverageTimePerFrame { get; set; }
    public int IncludedInNumGroups { get; set; }
    public int NumFramesCounted { get; set; }

    public Vector3D AveragePosition
    {
        get
        {
            var avgPos = Positions[0];

            for (int i = 1; i < Positions.Length; i++)
                avgPos += Positions[i];

            return avgPos / Positions.Length;
        }
    }

    public float AverageSpeed
    {
        get
        {
            var avgSpeed = Speeds[0];

            for (int i = 1; i < Speeds.Length; i++)
                avgSpeed += Speeds[i];

            return avgSpeed / Speeds.Length;
        }
    }

    public string GridTypeForColumn => IsStatic == true && GridSize == MyCubeSize.Large ? "Station" : GridSize.ToString();
    public string CustomNamesForColumn => CustomNames.Length == 1 ? CustomNames[0] : string.Join("\n", CustomNames);
    public string OwnerIDsForColumn => string.Join("\n", Owners.Select(o => o.ID));
    public string OwnerNamesForColumn => string.Join("\n", Owners.Select(o => o.Name));
    public string BlockCountsForColumn => BlockCounts.Length == 1 ? (BlockCounts[0] == 1 ? "1" : BlockCounts[0].ToString()) : string.Join(",\n", BlockCounts);
    public string PCUsForColumn => PCUs.Length == 1 ? PCUs[0].ToString() : string.Join(",\n", PCUs);

    public string SizesForColumn
    {
        get
        {
            return Sizes.Length == 1 ? VecToString(Sizes[0]) : string.Join(",\n", Sizes);

            static string VecToString(Vector3I vector) => $"X:{vector.X}, Y:{vector.Y}, Z:{vector.Z}";
        }
    }

    public string AveragePositionForColumn => Vector3D.Round(AveragePosition, 0).ToString();

    public string AverageSpeedForColumn
    {
        get
        {
            double speed = Math.Round(AverageSpeed);

            return speed == 0 ? "0" : speed.ToString();
        }
    }

    public string IsPoweredForColumn => IsPowered == null ? "*" : IsPowered.Value.ToString();

    public CubeGridAnalysisInfo(
        long entityId, MyCubeSize gridSize, bool? isStatic, bool isNpc,
        string[] customNames, (long ID, string? Name)[] owners,
        int[] blockCounts, int[] pcus, Vector3I[] sizes,
        Vector3D[] positions, float[] speeds, bool? isPowered,
        double totalTime, double averageTimePerFrame,
        int includedInNumGroups, int numFramesCounted)
    {
        EntityId = entityId;
        GridSize = gridSize;
        IsStatic = isStatic;
        IsNPC = isNpc;
        CustomNames = customNames;
        Owners = owners;
        BlockCounts = blockCounts;
        PCUs = pcus;
        Sizes = sizes;
        Positions = positions;
        Speeds = speeds;
        IsPowered = isPowered;

        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumGroups = includedInNumGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        return $"""
                {(IsStatic == true && GridSize == MyCubeSize.Large ? "Static" : GridSize)} Grid, ID: {EntityId}{(IsNPC ? "\n    IsNPC: true" : "")}
                    {(CustomNames.Length == 1 ? $"Custom Name: {CustomNames[0]}" : $"Custom Names: {string.Join(", ", CustomNames)}")}
                    Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", ID: " : null)}{o.ID})"))}
                    {(BlockCounts.Length == 1 ? $"Block Count: {BlockCounts[0]}" : $"Block Counts: {string.Join(", ", BlockCounts)}")}
                    {(PCUs.Length == 1 ? $"PCU: {PCUs[0]}" : $"PCUs: {string.Join(", ", PCUs)}")}
                    {(Sizes.Length == 1 ? $"Size: {Sizes[0]}" : $"Sizes: {string.Join(", ", Sizes)}")}
                    Avg. Position{Vector3D.Round(AveragePosition, 0)}
                    {(Speeds.Length == 1 ? $"Speed: {Speeds[0]}" : $"Speeds: {string.Join(", ", Speeds)}")}
                    IsPowered: {(IsPowered != null ? IsPowered : "*")}
                Total Time: {TotalTime:N1}ms
                Average Time: {AverageTimePerFrame:N2}ms
                Counted Frames: {NumFramesCounted}{(IncludedInNumGroups > 1 ? $"\r\nProcessed over {IncludedInNumGroups} threads" : "")}
                """;
    }
}

class CubeBlockAnalysisInfo
{
    public class Builder
    {
        public long EntityId;
        public MyCubeSize CubeSize;
        public HashSet<long> GridIds = [];
        public Type BlockType;
        public HashSet<string> CustomNames = [];
        public Dictionary<long, string?> Owners = [];
        public HashSet<Vector3D> Positions = [];
        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime;
        public double AverageTimePerFrame;

        public Builder(CubeBlockInfoProxy.Snapshot info)
        {
            EntityId = info.Block.EntityId;
            CubeSize = info.Grid.Grid.GridSize;
            BlockType = info.Block.BlockType.Type;

            Add(info);
        }

        public void Add(CubeBlockInfoProxy.Snapshot info)
        {
            if (info.CustomName != null)
                CustomNames.Add(info.CustomName);

            GridIds.Add(info.Grid.Grid.EntityId);
            Owners[info.OwnerId] = info.OwnerName;
            Positions.Add(info.Position);
        }

        public CubeBlockAnalysisInfo Finish()
        {
            return new CubeBlockAnalysisInfo(EntityId, CubeSize, GridIds.ToArray(), BlockType, CustomNames.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                Positions.ToArray(), TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public long EntityId { get; set; }
    public MyCubeSize CubeSize { get; set; }
    public long[] GridIds;
    public Type BlockType;
    public string[] CustomNames;
    public (long ID, string? Name)[] Owners;
    public Vector3D[] Positions;

    public double TotalTime { get; set; }
    public double AverageTimePerFrame { get; set; }
    public int IncludedInNumGroups { get; set; }
    public int NumFramesCounted { get; set; }

    public Vector3D AveragePosition
    {
        get
        {
            var avgPos = Positions[0];

            for (int i = 1; i < Positions.Length; i++)
                avgPos += Positions[i];

            return avgPos / Positions.Length;
        }
    }

    public string GridIdsForColumn => GridIds.Length == 1 ? GridIds[0].ToString() : string.Join("\n", GridIds);
    public string CustomNamesForColumn => CustomNames.Length == 1 ? CustomNames[0] : string.Join("\n", CustomNames);
    public string OwnerIDsForColumn => string.Join("\n", Owners.Select(o => o.ID));
    public string OwnerNamesForColumn => string.Join("\n", Owners.Select(o => o.Name));
    public string AveragePositionForColumn => Vector3D.Round(AveragePosition, 0).ToString();

    public CubeBlockAnalysisInfo(
        long entityId, MyCubeSize cubeSize, long[] gridIds, Type blockType,
        string[] customNames, (long ID, string? Name)[] owners,
        Vector3D[] positions,
        double totalTime, double averageTimePerFrame,
        int includedInNumGroups, int numFramesCounted)
    {
        EntityId = entityId;
        CubeSize = cubeSize;
        GridIds = gridIds;
        BlockType = blockType;
        CustomNames = customNames;
        Owners = owners;
        Positions = positions;
        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumGroups = includedInNumGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        return $"""
                {BlockType.Name}, ID: {EntityId}
                    {(GridIds.Length == 1 ? $"Grid ID: {GridIds[0]}" : $"Grid IDs: {string.Join(", ", GridIds)}")}
                    {(CustomNames.Length == 1 ? $"Custom Name: {CustomNames[0]}" : $"Custom Names: {string.Join(", ", CustomNames)}")}
                    Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", ID: " : null) + o.ID.ToString()})"))}
                    Avg. Position{Vector3D.Round(AveragePosition, 0)}
                Total Time: {TotalTime:N1}ms
                Average Time: {AverageTimePerFrame:N2}ms
                Counted Frames: {NumFramesCounted}{(IncludedInNumGroups > 1 ? $"\r\nProcessed over {IncludedInNumGroups} threads" : "")}
                """;
    }
}
