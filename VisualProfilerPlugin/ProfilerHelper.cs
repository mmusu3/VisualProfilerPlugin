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
using VRage;
using VRage.Game;
using VRage.Network;
using VRage.Utils;
using VRageMath;
using VRageMath.Spatial;
using GridGroup = VRage.Groups.MyGroups<Sandbox.Game.Entities.MyCubeGrid, Sandbox.Game.Entities.MyGridPhysicalGroupData>.Group;

namespace VisualProfiler;

static class ProfilerHelper
{
    internal static readonly IProfilerEventDataObjectResolver ProfilerEventObjectResolver = new ObjectResolver();

    class ObjectResolver : IProfilerEventDataObjectResolver
    {
        Dictionary<object, object> objectCache = [];
        Dictionary<MyCubeGrid, CubeGridInfoProxy> gridCache = [];
        Dictionary<MyCubeBlock, CubeBlockInfoProxy> blockCache = [];
        Dictionary<GridGroup, int> gridGroupsToIds = [];

        public void ClearCache()
        {
            objectCache.Clear();
            gridCache.Clear();
            blockCache.Clear();
            gridGroupsToIds.Clear();
        }

        public void Resolve(ref ProfilerEvent.ExtraData data)
        {
            switch (data.Object)
            {
            case GCEventInfo:
                break;
            case Type type:
                {
                    data.Format = "Type: {0}";

                    if (objectCache.TryGetValue(type, out var cachedObj))
                    {
                        data.Object = (string)cachedObj;
                        break;
                    }

                    objectCache[type] = data.Object = type.FullName!;
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

                    if (objectCache.TryGetValue(type, out var cachedObj))
                        data.Object = (string)cachedObj;
                    else
                        objectCache[type] = data.Object = type.FullName!;
                }
                break;
            case MyClusterTree.MyCluster cluster:
                {
                    data.Format = "{0}";

                    PhysicsClusterInfoProxy clusterInfo;

                    if (objectCache.TryGetValue(cluster, out var cachedObj))
                    {
                        clusterInfo = (PhysicsClusterInfoProxy)cachedObj;
                    }
                    else
                    {
                        clusterInfo = new PhysicsClusterInfoProxy(cluster);
                        objectCache.Add(cluster, clusterInfo);
                    }

                    data.Object = clusterInfo.GetSnapshot(cluster);
                }
                break;
            case MyCubeGrid grid:
                {
                    data.Format = "{0}";

                    CubeGridInfoProxy? gridInfo;

                    if (!gridCache.TryGetValue(grid, out gridInfo))
                    {
                        gridInfo = new CubeGridInfoProxy(grid);
                        gridCache.Add(grid, gridInfo);
                    }

                    data.Object = gridInfo.GetSnapshot(grid, gridGroupsToIds);
                }
                break;
            case MyCubeBlock block:
                {
                    data.Format = "{0}";

                    CubeBlockInfoProxy? blockInfo;

                    if (!blockCache.TryGetValue(block, out blockInfo))
                    {
                        blockInfo = new CubeBlockInfoProxy(block);
                        blockCache.Add(block, blockInfo);
                    }

                    var gridSnapshot = GetGridSnapshot(block.CubeGrid);

                    data.Object = blockInfo.GetSnapshot(gridSnapshot, block);
                }
                break;
            case MyCharacter character:
                {
                    data.Format = "{0}";

                    CharacterInfoProxy charInfo;

                    if (objectCache.TryGetValue(character, out var cachedObj))
                    {
                        charInfo = (CharacterInfoProxy)cachedObj;
                    }
                    else
                    {
                        charInfo = new CharacterInfoProxy(character);
                        objectCache.Add(character, charInfo);
                    }

                    data.Object = charInfo.GetSnapshot(character);
                }
                break;
            case MyFloatingObject floatingObj:
                {
                    data.Format = "{0}";

                    FloatingObjectInfoProxy floatObjInfo;

                    if (objectCache.TryGetValue(floatingObj, out var cachedObj))
                    {
                        floatObjInfo = (FloatingObjectInfoProxy)cachedObj;
                    }
                    else
                    {
                        floatObjInfo = new FloatingObjectInfoProxy(floatingObj);
                        objectCache.Add(floatingObj, floatObjInfo);
                    }

                    data.Object = floatObjInfo.GetSnapshot(floatingObj);
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

                    CubeGridInfoProxy? gridInfo;

                    if (!gridCache.TryGetValue(grid, out gridInfo))
                    {
                        gridInfo = new CubeGridInfoProxy(grid);
                        gridCache.Add(grid, gridInfo);
                    }

                    data.Object = gridInfo.GetSnapshot(grid, gridGroupsToIds);
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

                    CubeBlockInfoProxy? blockInfo;

                    if (!blockCache.TryGetValue(block, out blockInfo))
                    {
                        blockInfo = new CubeBlockInfoProxy(block);
                        blockCache.Add(block, blockInfo);
                    }

                    var gridSnapshot = GetGridSnapshot(block.CubeGrid);

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

                    if (objectCache.TryGetValue(character, out var cachedObj))
                    {
                        charInfo = (CharacterInfoProxy)cachedObj;
                    }
                    else
                    {
                        charInfo = new CharacterInfoProxy(character);
                        objectCache.Add(character, charInfo);
                    }

                    data.Object = charInfo.GetSnapshot(character);
                }
                break;
            case MyExternalReplicable<MyFloatingObject> floatObjRepl:
                {
                    var floatingObj = floatObjRepl.Instance;

                    if (floatingObj == null)
                    {
                        data.Object = null;
                        data.Format = "Empty floating object replicable{0}";
                        break;
                    }

                    data.Format = "{0}";

                    FloatingObjectInfoProxy floatObjInfo;

                    if (objectCache.TryGetValue(floatingObj, out var cachedObj))
                    {
                        floatObjInfo = (FloatingObjectInfoProxy)cachedObj;
                    }
                    else
                    {
                        floatObjInfo = new FloatingObjectInfoProxy(floatingObj);
                        objectCache.Add(floatingObj, floatObjInfo);
                    }

                    data.Object = floatObjInfo.GetSnapshot(floatingObj);
                }
                break;
            case MyExternalReplicable<MyVoxelBase> voxelRepl:
                {
                    data.Format = "{0}";

                    var voxel = voxelRepl.Instance;

                    if (voxel == null)
                    {
                        data.Object = null;
                        data.Object = "Empty voxel replicable{0}";
                        break;
                    }

                    if (objectCache.TryGetValue(voxel, out var cachedObj))
                    {
                        data.Object = (VoxelInfoProxy)cachedObj;
                        break;
                    }

                    objectCache[voxel] = data.Object = new VoxelInfoProxy(voxel);
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

        CubeGridInfoProxy.Snapshot GetGridSnapshot(MyCubeGrid grid)
        {
            CubeGridInfoProxy? gridInfo;

            if (!gridCache.TryGetValue(grid, out gridInfo))
            {
                gridInfo = new CubeGridInfoProxy(grid);
                gridCache.Add(grid, gridInfo);
            }

            return gridInfo.GetSnapshot(grid, gridGroupsToIds);
        }

        public void ResolveNonCached(ref ProfilerEvent.ExtraData data)
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
    }

    internal static void PrepareRecordingForSerialization(ProfilerEventsRecording recording)
    {
        var objsToIds = new Dictionary<object, int>();

        foreach (var (_, group) in recording.Groups)
        {
            for (int i = 0; i < group.Events.Length; i++)
            {
                ref var _event = ref group.Events[i];

                if (_event.ExtraValue.Type is not ProfilerEvent.ExtraValueTypeOption.Object
                    and not ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory)
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

        foreach (var (_, group) in recording.Groups)
        {
            for (int i = 0; i < group.Events.Length; i++)
            {
                ref var _event = ref group.Events[i];

                if (_event.NameKey != 0)
                    _event.NameKey = ProfilerKeyCache.GetOrAdd(recording.EventStrings[_event.NameKey]).GlobalIndex;

                if (_event.ExtraValue.Type is not ProfilerEvent.ExtraValueTypeOption.Object
                    and not ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory)
                    continue;

                int objId = _event.ExtraValue.ObjectKey.ID;

                if (objId == 0)
                {
                    // Null object
                }
                else if (objId < 0)
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
        var allBlocks = new Dictionary<long, CubeBlockAnalysisInfo.Builder>();
        var progBlocks = new Dictionary<long, CubeBlockAnalysisInfo.Builder>();
        var floatingObjs = new HashSet<long>();

        foreach (var (groupId, group) in recording.Groups)
        {
            if (group.FrameStartEventIndices.Length == 0
                || group.FrameEndEventIndices.Length == 0)
                continue;

            for (int f = 0; f < group.FrameStartEventIndices.Length; f++)
            {
                if (f >= group.FrameStartEventIndices.Length
                    || f >= group.FrameEndEventIndices.Length)
                    break;

                int startEventIndex = group.FrameStartEventIndices[f];
                int endEventIndex = group.FrameEndEventIndices[f];

                if (endEventIndex < startEventIndex)
                    continue;

                var events = group.Events.AsSpan(startEventIndex, endEventIndex + 1 - startEventIndex);

                {
                    long startTime = events[0].StartTime;
                    long endTime = events[^1].EndTime;
                    long frameTime = endTime - startTime;

                    while (frameTimes.Count <= f)
                        frameTimes.Add(0);

                    frameTimes[f] = Math.Max(frameTimes[f], frameTime);
                }

                for (int e = 0; e < events.Length; e++)
                {
                    ref var _event = ref events[e];

                    switch (_event.ExtraValue.Type)
                    {
                    case ProfilerEvent.ExtraValueTypeOption.Object:
                    case ProfilerEvent.ExtraValueTypeOption.ObjectAndCategory:
                        {
                            // TODO: Record list of event IDs per object for highlighting events in graph when object selected

                            // NOTE: This can analyze the same snapshots multiple times
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
                            case FloatingObjectInfoProxy.Snapshot floatObj:
                                floatingObjs.Add(floatObj.FloatingObj.EntityId);
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

        if (frameTimes.Count > 0)
        {
            frameTimeInfo.Mean /= frameTimes.Count;
            frameTimeInfo.StdDev = Math.Sqrt(frameTimeInfo.StdDev / frameTimes.Count - frameTimeInfo.Mean * frameTimeInfo.Mean);
        }
        else
        {
            frameTimeInfo.Min = 0;
            frameTimeInfo.StdDev = 0;
        }

        foreach (var item in clusters)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        foreach (var item in grids)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        foreach (var item in allBlocks)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        return new RecordingAnalysisInfo(frameTimeInfo,
            clusters.Values.Select(c => c.Finish()).ToArray(),
            grids.Values.Select(c => c.Finish()).ToArray(),
            progBlocks.Values.Select(c => c.Finish()).ToArray(),
            allBlocks.Count,
            floatingObjs.Count);

        void AnalyzePhysicsCluster(PhysicsClusterInfoProxy.Snapshot clusterInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (clusters.TryGetValue(clusterInfo.Cluster.ID, out var anInf))
                anInf.Add(clusterInfo);
            else
                clusters.Add(clusterInfo.Cluster.ID, anInf = new(clusterInfo));

            // TODO: Filter parent events to prevent time overlap
            //switch (_event.Name)
            //{
            //default:
            //    break;
            //}

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
            //switch (_event.Name)
            //{
            //default:
            //    break;
            //}

            anInf.TotalTime += _event.ElapsedMilliseconds;
            anInf.IncludedInGroups.Add(groupId);
            anInf.FramesCounted.Add(frameIndex);
        }

        void AnalyzeBlock(CubeBlockInfoProxy.Snapshot snapshot, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            long eid = snapshot.Block.EntityId;

            if (allBlocks.TryGetValue(eid, out var info))
                info.Add(snapshot);
            else
                allBlocks.Add(eid, info = new(snapshot));

            if (snapshot.Block.BlockType.Type == typeof(Sandbox.Game.Entities.Blocks.MyProgrammableBlock))
                progBlocks[eid] = info;

            // TODO: Filter parent events to prevent time overlap
            //switch (_event.Name)
            //{
            //default:
            //    break;
            //}

            info.TotalTime += _event.ElapsedMilliseconds;
            info.IncludedInProfilerGroups.Add(groupId);
            info.FramesCounted.Add(frameIndex);
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

    internal static CombinedFrameEvents CombineFrames(ProfilerEventsRecording recording)
    {
        var combinedGroups = new CombinedFrameEvents.Group[recording.Groups.Count];
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

            int startEventIndex = 0;
            int endEventIndex = -1;

            if (group.FrameStartEventIndices.Length > 0)
                startEventIndex = group.FrameStartEventIndices[0];

            if (group.FrameEndEventIndices.Length > 0)
                endEventIndex = group.FrameEndEventIndices[^1];

            var events = group.Events.AsSpan(startEventIndex, endEventIndex + 1 - startEventIndex);
            int prevDepth = 0;

            for (int e = 0; e < events.Length; e++)
            {
                ref var _event = ref events[e];

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

                var eventInfo = new CombinedEventInfo(timer);

                combinedEvents.Add(new ProfilerEvent {
                    NameKey = timer.NameKey.GlobalIndex,
                    Depth = depth,
                    StartTime = startTime,
                    EndTime = startTime + timer.ElapsedTime,
                    MemoryAfter = timer.AllocatedMemory,
                    Flags = timer.MemoryTracked ? ProfilerEvent.EventFlags.MemoryTracked : ProfilerEvent.EventFlags.None,
                    ExtraValue = timer.Category != ProfilerEvent.EventCategory.Other ? new(timer.Category, eventInfo, "{0}") : new(eventInfo, "{0}")
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

            combinedGroups[i++] = new() {
                ID = groupId,
                Time = groupTime,
                Events = combinedEvents.ToArray()
            };

            combinedEvents.Clear();
        }

        return new CombinedFrameEvents(maxTime, combinedGroups);

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
                case FloatingObjectInfoProxy.Snapshot:
                    return ProfilerEvent.EventCategory.FloatingObjects;
                }
            }

            return ProfilerEvent.EventCategory.Other;
        }
    }

    public static string SummarizeRecording(ProfilerEventsRecording recording)
    {
        var groups = CombineFrames(recording).Groups;

        var header = $"Recorded {recording.NumFrames} frames over {recording.ElapsedTime.TotalSeconds:N1} seconds.";

        if (groups.Length == 0)
            return header;

        var mainGroup = groups[0];
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

            var cat = t.Category != ProfilerEvent.EventCategory.FloatingObjects ? t.Category.ToString() : "FloatObjs";

            sb.Append(cat).Append(':').Append(' ', Math.Max(0, 10 - cat.Length)).Append("  ")
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

class CombinedFrameEvents
{
    public struct Group
    {
        public int ID;
        public long Time;
        public ProfilerEvent[] Events;
    }

    public long Time;
    public Group[] Groups;

    public CombinedFrameEvents(long time, Group[] groups)
    {
        Time = time;
        Groups = groups;
    }
}

[ProtoContract]
struct StringId
{
    [ProtoMember(1)] public int ID;

    [ProtoIgnore]
    public string? String
    {
        get
        {
            if (_string == null && ID > 0)
                _string = GeneralStringCache.Get(this);

            return _string;
        }
        set
        {
            _string = value;
            ID = GeneralStringCache.GetOrAdd(value).ID;
        }
    }
    string? _string;

    public StringId(int id)
    {
        ID = id;
    }

    public StringId(string? _string)
    {
        this._string = _string;
        ID = GeneralStringCache.GetOrAdd(_string).ID;
    }

    public override string? ToString() => String;

    public static implicit operator string?(StringId sid) => sid.String;
}

static class GeneralStringCache
{
    static readonly Dictionary<string, int> stringsToIds = [];
    static readonly Dictionary<int, string> idsToStrings = [];
    static readonly object lockObj = new();
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

        lock (lockObj)
        {
            if (!stringsToIds.TryGetValue(value, out id))
            {
                id = idGenerator++;
                stringsToIds.Add(value, id);
                idsToStrings.Add(id, value);
            }
        }

        return new StringId(id);
    }

    public static bool TryGet(string value, out StringId id)
    {
        int index;

        lock (lockObj)
        {
            if (!stringsToIds.TryGetValue(value, out index))
            {
                id = default;
                return false;
            }
        }

        id = new StringId(index);
        return true;
    }

    public static string? Get(StringId id)
    {
        if (id.ID == 0)
            return null;

        lock (lockObj)
            return idsToStrings[id.ID];
    }

    public static string? Intern(string? value)
    {
        if (value == null)
            return null;

        lock (lockObj)
        {
            if (stringsToIds.TryGetValue(value, out int id))
                return idsToStrings[id];

            id = idGenerator++;
            stringsToIds.Add(value, id);
            idsToStrings.Add(id, value);
        }

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
        TypeName = new StringId(Type?.AssemblyQualifiedName ?? "");
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
    // NOTE: ProtoMember IDs must be preserved. Fields are in display
    // order so Proto IDs are not sequential due to field additions.
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public MyCubeSize GridSize;
    [ProtoMember(4)] public bool IsNPC;
    [ProtoMember(5)] public bool IsPreview;
    [ProtoMember(3)] public List<RefObjWrapper<Snapshot>> Snapshots = [];

    [ProtoContract]
    public class Snapshot
    {
        // NOTE: ProtoMember IDs must be preserved. Fields are in display
        // order so Proto IDs are not sequential due to field additions.
        [ProtoMember(1, AsReference = true)] public CubeGridInfoProxy Grid;
        [ProtoMember(2)] public ulong FrameIndex;
        [ProtoMember(8)] public bool IsStatic;
        [ProtoMember(3)] public string Name;
        [ProtoMember(4)] public long OwnerId;
        [ProtoMember(5)] public StringId OwnerName;
        [ProtoMember(6)] public int BlockCount;
        [ProtoMember(7)] public Vector3D Position;
        [ProtoMember(13)] public int PhysicsCluster;
        [ProtoMember(9)] public float Speed;
        [ProtoMember(10)] public Vector3I Size;
        [ProtoMember(11)] public int PCU;
        [ProtoMember(12)] public bool IsPowered;
        [ProtoMember(14)] public int ConnectedGrids;
        [ProtoMember(15)] public int GroupId;
        [ProtoMember(16)] public int GroupSize;

        public Snapshot(CubeGridInfoProxy gridInfo, MyCubeGrid grid, Dictionary<GridGroup, int> gridGroupsToIds)
        {
            Grid = gridInfo;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;
            IsStatic = grid.IsStatic;

            long ownerId = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            Name = grid.DisplayName;
            OwnerId = ownerId;
            OwnerName = new StringId(ownerIdentity?.DisplayName);
            BlockCount = grid.BlocksCount;
            PCU = grid.BlocksPCU;
            Size = grid.Max - grid.Min + Vector3I.One;
            Position = grid.PositionComp.GetPosition();
            PhysicsCluster = PhysicsHelper.GetClusterIdForObject(grid.Physics);
            Speed = grid.LinearVelocity.Length();
            IsPowered = grid.IsPowered;

            var groupNode = MyCubeGridGroups.Static.Physical.GetNode(grid);

            if (!gridGroupsToIds.TryGetValue(groupNode.Group, out GroupId))
                gridGroupsToIds.Add(groupNode.Group, GroupId = gridGroupsToIds.Count + 1);

            GroupSize = groupNode.Group.Nodes.Count;
            ConnectedGrids = groupNode.LinkCount;
        }

        public Snapshot()
        {
            Grid = null!;
            Name = "";
            PhysicsCluster = -1;
        }

        public bool Equals(MyCubeGrid grid, Dictionary<GridGroup, int> gridGroupsToIds)
        {
            // Grid info is captured at the end of the frame. State changes within the frame are not seen.
            if (MySandboxGame.Static.SimulationFrameCounter == FrameIndex)
                return true;

            long ownerId = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
            var groupNode = MyCubeGridGroups.Static.Physical.GetNode(grid);

            return IsStatic == grid.IsStatic
                && Name == grid.DisplayName
                && OwnerId == ownerId
                && BlockCount == grid.BlocksCount
                && PCU == grid.BlocksPCU
                && Size == (grid.Max - grid.Min + Vector3I.One)
                && Vector3D.DistanceSquared(Position, grid.PositionComp.GetPosition()) < (0.1 * 0.1)
                && PhysicsCluster == PhysicsHelper.GetClusterIdForObject(grid.Physics)
                && Math.Abs(Speed - grid.LinearVelocity.Length()) < 0.1
                && IsPowered == grid.IsPowered
                && GroupId == gridGroupsToIds.GetValueOrDefault(groupNode.Group, -1)
                && GroupSize == groupNode.Group.Nodes.Count
                && ConnectedGrids == groupNode.LinkCount;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (IsStatic && Grid.GridSize == MyCubeSize.Large)
                sb.Append("Static");
            else
                sb.Append(Grid.GridSize);

            sb.AppendLine($" Grid, ID: {Grid.EntityId}");

            if (Grid.IsNPC)
                sb.AppendLine("    Is NPC: True");

            if (Grid.IsPreview)
                sb.AppendLine($"    Is Preview: {Grid.IsPreview}");

            var idPart = OwnerName.String != null ? $", ID: " : null;

            sb.AppendLine($"    Name: {Name}");
            sb.AppendLine($"    Owner: {OwnerName}{idPart}{OwnerId}");
            sb.AppendLine($"    Blocks: {BlockCount}");
            sb.AppendLine($"    PCU: {PCU}");
            sb.AppendLine($"    Size: {Size}");
            sb.AppendLine($"    Position: {Vector3D.Round(Position, 0)}");
            sb.AppendLine($"    Physics Cluster: {PhysicsCluster}");

            float roundSpeed = (float)Math.Round(Speed, 1);

            if (roundSpeed > 0)
            {
                if (Speed < 0.1f)
                    sb.AppendLine($"    Speed: <0.1");
                else
                    sb.AppendLine($"    Speed: {roundSpeed}");
            }

            sb.AppendLine($"    Is Powered: {IsPowered}");

            if (GroupSize > 1)
            {
                sb.AppendLine($"    Group ID: {GroupId}");
                sb.AppendLine($"    Group Size: {GroupSize}");
                sb.AppendLine($"    Connected Grid Count: {ConnectedGrids}");
            }

            return sb.ToString();
        }
    }

    public CubeGridInfoProxy(MyCubeGrid grid)
    {
        EntityId = grid.EntityId;
        GridSize = grid.GridSizeEnum;
        IsNPC = grid.IsNpcSpawnedGrid;
        IsPreview = grid.IsPreview;
    }

    public CubeGridInfoProxy()
    {
    }

    public Snapshot GetSnapshot(MyCubeGrid grid, Dictionary<GridGroup, int> gridGroupsToIds)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1].Object : null;

        if (lastSnapshot != null && lastSnapshot.Equals(grid, gridGroupsToIds))
            return lastSnapshot;

        var snapshot = new Snapshot(this, grid, gridGroupsToIds);

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
        [ProtoMember(6)] public StringId OwnerName;
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
            OwnerName = new StringId(ownerIdentity?.DisplayName);
            Position = block.PositionComp.GetPosition();
        }

        public Snapshot()
        {
            Grid = null!;
            Block = null!;
        }

        public bool Equals(MyCubeBlock block)
        {
            // Grid info is captured at the end of the frame. State changes within the frame are not seen.
            if (MySandboxGame.Static.SimulationFrameCounter == FrameIndex)
                return true;

            return Grid.Grid.EntityId == block.CubeGrid.EntityId
                && OwnerId == block.OwnerId
                && Equals((block as MyTerminalBlock)?.CustomName, CustomName);

            static bool Equals(StringBuilder? sb, string? s)
            {
                if (sb == null)
                {
                    return s == null;
                }
                else if (s == null)
                {
                    return false;
                }

#if NET
                return sb.Equals(s);
#else

                if (sb.Length != s.Length)
                    return false;

                for (int i = 0; i < s.Length; i++)
                {
                    if (sb[i] != s[i])
                        return false;
                }

                return true;
#endif
            }
        }

        public override string ToString()
        {
            var idPart = OwnerName.String != null ? $", ID: " : null;

            return $"""
                {Block.BlockType.Type.Name}, ID: {Block.EntityId}
                   Name: {CustomName}
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

        public bool Equals(MyCharacter character)
        {
            return Vector3D.DistanceSquared(Position, character.PositionComp.GetPosition()) < (0.1 * 0.1);
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
class FloatingObjectInfoProxy
{
    [ProtoMember(1)] public long EntityId;
    [ProtoMember(2)] public StringId ItemTypeId;
    [ProtoMember(3)] public StringId ItemSubtypeId;
    [ProtoMember(4)] public List<RefObjWrapper<Snapshot>> Snapshots = [];

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public FloatingObjectInfoProxy FloatingObj;
        [ProtoMember(2)] public Vector3D Position;
        [ProtoMember(3)] public MyFixedPoint Amount;

        public Snapshot(FloatingObjectInfoProxy objInfo, MyFloatingObject floatingObj)
        {
            FloatingObj = objInfo;
            Position = floatingObj.PositionComp.GetPosition();
            Amount = floatingObj.Amount;
        }

        public Snapshot()
        {
            FloatingObj = null!;
        }

        public bool Equals(MyFloatingObject floatingObj)
        {
            return Amount == floatingObj.Amount
                && Vector3D.DistanceSquared(Position, floatingObj.PositionComp.GetPosition()) < (0.1 * 0.1);
        }

        public override string ToString()
        {
            return $"""
                Floating Object, ID: {FloatingObj.EntityId}
                   Item ID: {FloatingObj.ItemTypeId}/{FloatingObj.ItemSubtypeId}
                   Amount: {Amount}
                   Position: {Vector3D.Round(Position, 1)}
                """;
        }
    }

    public FloatingObjectInfoProxy(MyFloatingObject floatingObj)
    {
        EntityId = floatingObj.EntityId;
        ItemTypeId = new StringId(floatingObj.ItemDefinition.Id.TypeId.ToString());
        ItemSubtypeId = new StringId(floatingObj.ItemDefinition.Id.SubtypeId.String);
    }

    public FloatingObjectInfoProxy()
    {
    }

    public Snapshot GetSnapshot(MyFloatingObject floatingObj)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1].Object : null;

        if (lastSnapshot != null && lastSnapshot.Equals(floatingObj))
            return lastSnapshot;

        var snapshot = new Snapshot(this, floatingObj);

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
    public int TotalBlocks;
    public int FloatingObjects;

    internal RecordingAnalysisInfo(FrameTimeInfo frameTimes, PhysicsClusterAnalysisInfo[] physicsClusters,
        CubeGridAnalysisInfo[] grids, CubeBlockAnalysisInfo[] programmableBlocks, int totalBlocks, int floatingObjs)
    {
        FrameTimes = frameTimes;
        PhysicsClusters = physicsClusters;
        Grids = grids;
        ProgrammableBlocks = programmableBlocks;
        TotalBlocks = totalBlocks;
        FloatingObjects = floatingObjs;
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

            var minExt = Vector3D.Round(AABBs[minIndex].Size / 1000, 1);
            var maxExt = Vector3D.Round(AABBs[maxIndex].Size / 1000, 1);

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

        var sb = new StringBuilder();

        sb.AppendLine($" Physics Cluster, ID: {ID}");

        if (AABBs.Length == 1)
            sb.AppendLine($"    {ToString(Round(AABBs[0]))}");
        else
            sb.AppendLine($"    AABBs: {string.Join(", ", AABBs.Select(Round).Distinct().Select(ToString2))}");

        sb.AppendLine($"    Num Objects{(minObjects == maxObjects ? $": {maxObjects}" : $", Min: {minObjects}, Max: {maxObjects}")}");
        sb.AppendLine($"    Num Active Objects{(minActiveObjects == maxActiveObjects ? $": {maxActiveObjects}" : $", Min: {minActiveObjects}, Max: {maxActiveObjects}")}");
        sb.AppendLine($"    Num Characters{(minCharacters == maxCharacters ? $": {maxCharacters}" : $", Min: {minCharacters}, Max: {maxCharacters}")}");

        sb.AppendLine($"Total Time: {TotalTime:N1}ms");
        sb.AppendLine($"Average Time: {AverageTimePerFrame:N2}ms");
        sb.AppendLine($"Counted Frames: {NumFramesCounted}");

        if (IncludedInNumGroups > 1)
            sb.AppendLine($"Processed over {IncludedInNumGroups} threads");

        return sb.ToString();

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
        HashSet<CubeGridInfoProxy.Snapshot> snapshots = [];

        public long EntityId;
        public MyCubeSize GridSize;
        public bool IsNPC;
        public bool IsPreview;
        public bool? IsStatic; // Null value means mixed
        public HashSet<string> Names = [];
        public Dictionary<long, string?> Owners = [];
        public HashSet<int> BlockCounts = [];
        public HashSet<int> PCUs = [];
        public HashSet<Vector3I> Sizes = [];
        public HashSet<Vector3D> Positions = [];
        public HashSet<int> PhysicsClusters = [];
        public HashSet<float> Speeds = [];
        public bool? IsPowered; // Null value means mixed
        public HashSet<int> GroupIds = [];
        public HashSet<int> GroupSizes = [];
        public HashSet<int> ConnectedGrids = [];
        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime;
        public double AverageTimePerFrame;

        public Builder(CubeGridInfoProxy.Snapshot snapshot)
        {
            EntityId = snapshot.Grid.EntityId;
            GridSize = snapshot.Grid.GridSize;
            IsNPC = snapshot.Grid.IsNPC;
            IsPreview = snapshot.Grid.IsPreview;
            IsStatic = snapshot.IsStatic;
            IsPowered = snapshot.IsPowered;

            Add(snapshot);
        }

        public void Add(CubeGridInfoProxy.Snapshot snapshot)
        {
            snapshots.Add(snapshot);

            if (IsStatic != null && snapshot.IsStatic != IsStatic)
                IsStatic = null;

            Names.Add(snapshot.Name);
            Owners[snapshot.OwnerId] = snapshot.OwnerName;
            BlockCounts.Add(snapshot.BlockCount);
            PCUs.Add(snapshot.PCU);
            Sizes.Add(snapshot.Size);
            Positions.Add(snapshot.Position);
            PhysicsClusters.Add(snapshot.PhysicsCluster);
            Speeds.Add(snapshot.Speed);

            if (IsPowered != null && snapshot.IsPowered != IsPowered)
                IsPowered = null;

            GroupIds.Add(snapshot.GroupId);
            GroupSizes.Add(snapshot.GroupSize);
            ConnectedGrids.Add(snapshot.ConnectedGrids);
        }

        public CubeGridAnalysisInfo Finish()
        {
            return new CubeGridAnalysisInfo(snapshots.Count, EntityId, GridSize, IsNPC, IsPreview, IsStatic, Names.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                BlockCounts.ToArray(), PCUs.ToArray(), Sizes.ToArray(), Positions.ToArray(), PhysicsClusters.ToArray(), Speeds.ToArray(), IsPowered, GroupIds.ToArray(), GroupSizes.ToArray(), ConnectedGrids.ToArray(),
                TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public int SnapshotCount { get; }
    public long EntityId { get; }
    public MyCubeSize GridSize;
    public bool IsNPC;
    public bool IsPreview { get; }
    public bool? IsStatic;
    public string[] Names;
    public (long ID, string? Name)[] Owners;
    public int[] BlockCounts;
    public int[] PCUs;
    public Vector3I[] Sizes;
    public Vector3D[] Positions;
    public int[] PhysicsClusters;
    public float[] Speeds;
    public bool? IsPowered;
    public int[] GroupIds;
    public int[] GroupSizes;
    public int[] ConnectedGrids;

    public double TotalTime { get; }
    public double AverageTimePerFrame { get; }
    public int IncludedInNumProfilerGroups { get; }
    public int NumFramesCounted { get; }

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
    public string NamesForColumn => Names.Length == 1 ? Names[0] : string.Join("\n", Names);
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
    public string PhysicsClustersForColumn => PhysicsClusters.Length == 1 ? PhysicsClusters[0].ToString() : string.Join(",\n", PhysicsClusters);

    public string AverageSpeedForColumn
    {
        get
        {
            double speed = AverageSpeed;

            if (speed > 0 && speed < 0.1f)
                return "< 0.1";

            speed = Math.Round(speed, 1);

            return speed == 0 ? "0" : speed.ToString();
        }
    }

    public string IsPoweredForColumn => IsPowered == null ? "*" : IsPowered.Value.ToString();
    public string GroupIdForColumn => GroupIds.Length == 1 ? GroupIds[0].ToString() : string.Join(",\n", GroupIds);
    public string GroupSizeForColumn => GroupSizes.Length == 1 ? GroupSizes[0].ToString() : string.Join(",\n", GroupSizes);
    public string ConnectedGridsForColumn => ConnectedGrids.Length == 1 ? ConnectedGrids[0].ToString() : string.Join(",\n", ConnectedGrids);

    public CubeGridAnalysisInfo(
        int snapshotCount, long entityId, MyCubeSize gridSize, bool isNpc, bool isPreview, bool? isStatic,
        string[] names, (long ID, string? Name)[] owners,
        int[] blockCounts, int[] pcus, Vector3I[] sizes,
        Vector3D[] positions, int[] physicsClusters, float[] speeds, bool? isPowered, int[] groupIds, int[] groupSizes, int[] connectedGrids,
        double totalTime, double averageTimePerFrame,
        int includedInNumProfilerGroups, int numFramesCounted)
    {
        SnapshotCount = snapshotCount;
        EntityId = entityId;
        GridSize = gridSize;
        IsNPC = isNpc;
        IsPreview = isPreview;
        IsStatic = isStatic;
        Names = names;
        Owners = owners;
        BlockCounts = blockCounts;
        PCUs = pcus;
        Sizes = sizes;
        Positions = positions;
        PhysicsClusters = physicsClusters;
        Speeds = speeds;
        IsPowered = isPowered;
        GroupIds = groupIds;
        GroupSizes = groupSizes;
        ConnectedGrids = connectedGrids;

        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumProfilerGroups = includedInNumProfilerGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        if (IsStatic == true && GridSize == MyCubeSize.Large)
            sb.Append("Static");
        else
            sb.Append(GridSize);

        sb.AppendLine($" Grid, ID: {EntityId}");

        if (IsNPC)
            sb.AppendLine("    Is NPC: True");

        if (IsPreview)
            sb.AppendLine("    Is Preview: True");

        if (Names.Length == 1)
            sb.AppendLine($"    Name: {Names[0]}");
        else
            sb.AppendLine($"    Names: {string.Join(", ", Names)}");

        sb.AppendLine($"    Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", ID: " : null)}{o.ID})"))}");

        if (BlockCounts.Length == 1)
            sb.AppendLine($"    Block Count: {BlockCounts[0]}");
        else
            sb.AppendLine($"    Block Counts: {string.Join(", ", BlockCounts)}");

        if (PCUs.Length == 1)
            sb.AppendLine($"    PCU: {PCUs[0]}");
        else
            sb.AppendLine($"    PCUs: {string.Join(", ", PCUs)}");

        if (Sizes.Length == 1)
            sb.AppendLine($"    Size: {Sizes[0]}");
        else
            sb.AppendLine($"    Sizes: {string.Join(", ", Sizes)}");

        sb.AppendLine($"    Avg. Position{Vector3D.Round(AveragePosition, 0)}");

        if (PhysicsClusters.Length == 1)
            sb.AppendLine($"    Physics Cluster ID: {PhysicsClusters[0]}");
        else
            sb.AppendLine($"    Physics Cluster IDs: {string.Join(", ", PhysicsClusters)}");

        if (Speeds.Length == 1)
        {
            if (Speeds[0] != 0)
                sb.AppendLine($"    Speed: {Speeds[0]}");
        }
        else
        {
            sb.AppendLine($"    Speeds: {string.Join(", ", Speeds)}");
        }

        if (GroupIds.Length == 1)
        {
            if (GroupIds[0] != 0)
                sb.AppendLine($"    Group ID: {GroupIds[0]}");
        }
        else
        {
            sb.AppendLine($"    Group IDs: {string.Join(", ", GroupIds)}");
        }

        if (GroupSizes.Length == 1)
        {
            if (GroupSizes[0] != 0)
                sb.AppendLine($"    Group Size: {GroupSizes[0]}");
        }
        else
        {
            sb.AppendLine($"    Group Sizes: {string.Join(", ", GroupSizes)}");
        }

        if (ConnectedGrids.Length == 1)
        {
            if (ConnectedGrids[0] != 0)
                sb.AppendLine($"    Connected Grid Count: {ConnectedGrids[0]}");
        }
        else
        {
            sb.AppendLine($"    Connected Grid Counts: {string.Join(", ", ConnectedGrids)}");
        }

        sb.AppendLine($"    IsPowered: {(IsPowered != null ? IsPowered : "*")}");
        sb.AppendLine($"Total Time: {TotalTime:N1}ms");
        sb.AppendLine($"Average Time: {AverageTimePerFrame:N2}ms");
        sb.AppendLine($"Counted Frames: {NumFramesCounted}");

        if (IncludedInNumProfilerGroups > 1)
            sb.AppendLine($"Processed over {IncludedInNumProfilerGroups} threads");

        return sb.ToString();
    }
}

class CubeBlockAnalysisInfo
{
    public class Builder
    {
        HashSet<CubeBlockInfoProxy.Snapshot> snapshots = [];

        public long EntityId;
        public MyCubeSize CubeSize;
        public HashSet<long> GridIds = [];
        public Type BlockType;
        public HashSet<string> CustomNames = [];
        public Dictionary<long, string?> Owners = [];
        public HashSet<Vector3D> Positions = [];
        public HashSet<int> IncludedInProfilerGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime;
        public double AverageTimePerFrame;

        public Builder(CubeBlockInfoProxy.Snapshot snapshot)
        {
            EntityId = snapshot.Block.EntityId;
            CubeSize = snapshot.Grid.Grid.GridSize;
            BlockType = snapshot.Block.BlockType.Type;

            Add(snapshot);
        }

        public void Add(CubeBlockInfoProxy.Snapshot snapshot)
        {
            snapshots.Add(snapshot);

            if (snapshot.CustomName != null)
                CustomNames.Add(snapshot.CustomName);

            GridIds.Add(snapshot.Grid.Grid.EntityId);
            Owners[snapshot.OwnerId] = snapshot.OwnerName;
            Positions.Add(snapshot.Position);
        }

        public CubeBlockAnalysisInfo Finish()
        {
            return new CubeBlockAnalysisInfo(snapshots.Count, EntityId, CubeSize, GridIds.ToArray(), BlockType, CustomNames.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                Positions.ToArray(), TotalTime, AverageTimePerFrame, IncludedInProfilerGroups.Count, FramesCounted.Count);
        }
    }

    public int SnapshotCount { get; }
    public long EntityId { get; }
    public MyCubeSize CubeSize { get; }
    public long[] GridIds;
    public Type BlockType;
    public string[] CustomNames;
    public (long ID, string? Name)[] Owners;
    public Vector3D[] Positions;

    public double TotalTime { get; }
    public double AverageTimePerFrame { get; }
    public int IncludedInNumProfilerGroups { get; }
    public int NumFramesCounted { get; }

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
        int snapshotCount, long entityId, MyCubeSize cubeSize, long[] gridIds, Type blockType,
        string[] customNames, (long ID, string? Name)[] owners,
        Vector3D[] positions,
        double totalTime, double averageTimePerFrame,
        int includedInNumProfilerGroups, int numFramesCounted)
    {
        SnapshotCount = snapshotCount;
        EntityId = entityId;
        CubeSize = cubeSize;
        GridIds = gridIds;
        BlockType = blockType;
        CustomNames = customNames;
        Owners = owners;
        Positions = positions;
        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumProfilerGroups = includedInNumProfilerGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($" {BlockType.Name}, ID: {EntityId}");

        if (GridIds.Length == 1)
            sb.AppendLine($"    Grid ID: {GridIds[0]}");
        else
            sb.AppendLine($"    Grid IDs: {string.Join(", ", GridIds)}");

        if (CustomNames.Length == 1)
            sb.AppendLine($"    Custom Name: {CustomNames[0]}");
        else
            sb.AppendLine($"    Custom Names: {string.Join(", ", CustomNames)}");

        if (Owners.Length == 1)
        {
            var owner = Owners[0];

            if (owner.Name != null)
                sb.AppendLine($"    Owner: {owner.Name}, ID: {Owners[0].ID}");
            else
                sb.AppendLine($"    Owner: {Owners[0].ID}");
        }
        else
        {
            sb.AppendLine($"    Owners: {string.Join(", ", Owners.Select(o => OwnerToString(o)))}");

            static string OwnerToString((long ID, string? Name) owner)
                => $"({owner.Name}{(owner.Name != null ? $", ID: " : null)}{owner.ID})";
        }

        sb.AppendLine($"    Avg. Position{Vector3D.Round(AveragePosition, 0)}");

        sb.AppendLine($"Total Time: {TotalTime:N1}ms");
        sb.AppendLine($"Average Time: {AverageTimePerFrame:N2}ms");
        sb.AppendLine($"Counted Frames: {NumFramesCounted}");

        if (IncludedInNumProfilerGroups > 1)
            sb.AppendLine($"Processed over {IncludedInNumProfilerGroups} threads");

        return sb.ToString();
    }
}
