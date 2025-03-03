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

        public void Resolve(ref ProfilerEvent _event)
        {
            ref var obj = ref _event.DataObject;
            ref var format = ref _event.DataFormat;

            switch (obj)
            {
            case GCEventInfo:
                break;
            case Type type:
                {
                    format = "Type: {0}";

                    if (objectCache.TryGetValue(type, out var cachedObj))
                    {
                        obj = (string)cachedObj;
                        break;
                    }

                    objectCache[type] = obj = type.FullName!;
                }
                break;
            case Delegate @delegate:
                {
                    format = "Declaring Type: {0}";

                    Type type;

                    // TODO: Get more info from Target
                    if (@delegate.Target != null)
                        type = @delegate.Target.GetType();
                    else
                        type = @delegate.Method.DeclaringType!;

                    if (objectCache.TryGetValue(type, out var cachedObj))
                        obj = (string)cachedObj;
                    else
                        objectCache[type] = obj = type.FullName!;
                }
                break;
            case MyClusterTree.MyCluster cluster:
                {
                    format = "{0}";

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

                    obj = clusterInfo.GetSnapshot(cluster);
                }
                break;
            case MyCubeGrid grid:
                {
                    format = "{0}";
                    obj = GetGridSnapshot(grid).GetMotionSnapshot(grid);
                }
                break;
            case MyCubeBlock block:
                {
                    format = "{0}";

                    CubeBlockInfoProxy? blockInfo;

                    if (!blockCache.TryGetValue(block, out blockInfo))
                    {
                        blockInfo = new CubeBlockInfoProxy(block);
                        blockCache.Add(block, blockInfo);
                    }

                    var gridSnapshot = GetGridSnapshot(block.CubeGrid);

                    obj = blockInfo.GetSnapshot(gridSnapshot, block);
                }
                break;
            case MyCharacter character:
                {
                    format = "{0}";

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

                    obj = charInfo.GetSnapshot(character);
                }
                break;
            case MyFloatingObject floatingObj:
                {
                    format = "{0}";

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

                    obj = floatObjInfo.GetSnapshot(floatingObj);
                }
                break;
            case MyExternalReplicable<MyCubeGrid> gridRepl:
                {
                    var grid = gridRepl.Instance;

                    if (grid == null)
                    {
                        obj = null;
                        format = "Empty cube grid replicable{0}";
                        break;
                    }

                    format = "{0}";
                    obj = GetGridSnapshot(grid).GetMotionSnapshot(grid);
                }
                break;
            case MyExternalReplicable<MySyncedBlock> blockRepl:
                {
                    var block = blockRepl.Instance;

                    if (block == null)
                    {
                        obj = null;
                        format = "Empty cube block replicable{0}";
                        break;
                    }

                    format = "{0}";

                    CubeBlockInfoProxy? blockInfo;

                    if (!blockCache.TryGetValue(block, out blockInfo))
                    {
                        blockInfo = new CubeBlockInfoProxy(block);
                        blockCache.Add(block, blockInfo);
                    }

                    var gridSnapshot = GetGridSnapshot(block.CubeGrid);

                    obj = blockInfo.GetSnapshot(gridSnapshot, block);
                }
                break;
            case MyExternalReplicable<MyCharacter> charRepl:
                {
                    var character = charRepl.Instance;

                    if (character == null)
                    {
                        obj = null;
                        format = "Empty character replicable{0}";
                        break;
                    }

                    format = "{0}";

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

                    obj = charInfo.GetSnapshot(character);
                }
                break;
            case MyExternalReplicable<MyFloatingObject> floatObjRepl:
                {
                    var floatingObj = floatObjRepl.Instance;

                    if (floatingObj == null)
                    {
                        obj = null;
                        format = "Empty floating object replicable{0}";
                        break;
                    }

                    format = "{0}";

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

                    obj = floatObjInfo.GetSnapshot(floatingObj);
                }
                break;
            case MyExternalReplicable<MyVoxelBase> voxelRepl:
                {
                    var voxel = voxelRepl.Instance;

                    if (voxel == null)
                    {
                        obj = null;
                        format = "Empty voxel replicable{0}";
                        break;
                    }

                    format = "{0}";

                    if (objectCache.TryGetValue(voxel, out var cachedObj))
                    {
                        obj = (VoxelInfoProxy)cachedObj;
                        break;
                    }

                    objectCache[voxel] = obj = new VoxelInfoProxy(voxel);
                }
                break;
            case IMyReplicable replicable:
                {
                    obj = null;
                    format = replicable.GetType().Name;
                }
                break;
            default:
                obj = GeneralStringCache.Intern(obj?.ToString());
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

        public void ResolveNonCached(ref ProfilerEvent _event)
        {
            ref var obj = ref _event.DataObject;
            ref var format = ref _event.DataFormat;

            switch (obj)
            {
            case GCEventInfo:
                break;
            case Type type:
                {
                    format = "Type: {0}";
                    obj = type.FullName!;
                }
                break;
            case Delegate @delegate:
                {
                    format = "Declaring Type: {0}";

                    Type type;

                    // TODO: Get more info from Target
                    if (@delegate.Target != null)
                        type = @delegate.Target.GetType();
                    else
                        type = @delegate.Method.DeclaringType!;

                    obj = type.FullName!;
                }
                break;
            default:
                obj = GeneralStringCache.Intern(obj?.ToString());
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

                if (_event.DataType
                    is not ProfilerEvent.DataTypeOption.Object
                    and not ProfilerEvent.DataTypeOption.ObjectAndCategory)
                    continue;

                _event.DataObjectKey = GetObjId(_event.DataObject);
            }
        }

        ObjectId GetObjId(object? obj)
        {
            if (obj == null)
                return new(0);

            if (obj is string str)
                return new(-GeneralStringCache.GetOrAdd(str).ID);

            if (!objsToIds.TryGetValue(obj, out int id))
            {
                id = 1 + objsToIds.Count;
                objsToIds.Add(obj, id);
                recording.DataObjects.Add(id, new(obj));
            }

            return new(id);
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

                if (_event.DataType is not ProfilerEvent.DataTypeOption.Object
                    and not ProfilerEvent.DataTypeOption.ObjectAndCategory)
                    continue;

                int objId = _event.DataObjectKey.ID;

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

                    _event.DataObject = str;
                }
                else
                {
                    if (!recording.DataObjects.TryGetValue(objId, out var obj))
                    {
                        // Assert?
                    }

                    _event.DataObject = obj.Object;
                }
            }
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

    struct CustomKey : IEquatable<CustomKey>
    {
        public int BaseKey;
        public string CustomName;

        public CustomKey(int baseKey, string customName)
        {
            BaseKey = baseKey;
            CustomName = customName;
        }

        public readonly bool Equals(CustomKey other) => BaseKey == other.BaseKey && CustomName == other.CustomName;
        public override readonly bool Equals(object? obj) => obj is CustomKey other && Equals(other);
        public override readonly int GetHashCode() => (BaseKey * 397) ^ CustomName.GetHashCode();
    }

    internal static CombinedFrameEvents CombineFrames(ProfilerEventsRecording recording)
    {
        var combinedGroups = new CombinedFrameEvents.Group[recording.Groups.Count];
        var combinedEvents = new List<ProfilerEvent>();
        var timers = new Dictionary<int, AccumTimer>();
        var customKeys = new Dictionary<CustomKey, int>();

        AccumTimer? activeTimer;

        AccumTimer GetOrAddTimer(ProfilerKey key, int depthDir)
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
                if (!activeTimer.Children.TryGetValue(key.GlobalIndex, out var subTimer))
                {
                    subTimer = new(key) { Parent = activeTimer };
                    activeTimer.Children.Add(key.GlobalIndex, subTimer);
                }

                return subTimer;
            }
            else
            {
                if (!timers.TryGetValue(key.GlobalIndex, out var timer))
                {
                    timer = new(key);
                    timers.Add(key.GlobalIndex, timer);
                }

                return timer;
            }
        }

        long maxTime = long.MinValue;
        int i = 0;

        foreach (var (groupId, group) in recording.Groups)
        {
            activeTimer = null;

            var events = group.GetAllFrameEvents();
            int prevDepth = 0;

            for (int e = 0; e < events.Length; e++)
            {
                ref var _event = ref events[e];

                if (_event.IsSinglePoint)
                    continue;

                long elapsedTime = _event.EndTime - _event.StartTime;
                long allocdMem = _event.MemoryAfter - _event.MemoryBefore;

                var category = GetCategory(ref _event);
                var nameKey = new ProfilerKey(_event.NameKey);

                if (Patches.MyParallelEntityUpdateOrchestrator_Patches.UseLiteProfiling)
                {
                    if (category == ProfilerEvent.EventCategory.Blocks)
                    {
                        if (_event.DataType
                            is ProfilerEvent.DataTypeOption.Object
                            or ProfilerEvent.DataTypeOption.ObjectAndCategory)
                        {
                            if (_event.DataObject is CubeBlockInfoProxy.Snapshot block)
                                nameKey = ProfilerKeyCache.GetOrAdd(block.Block.BlockType.Type.Name);
                        }
                    }
                    else if (category == ProfilerEvent.EventCategory.Grids)
                    {
                        if (_event.DataType
                            is ProfilerEvent.DataTypeOption.Object
                            or ProfilerEvent.DataTypeOption.ObjectAndCategory)
                        {
                            if (_event.DataObject is CubeGridInfoProxy.MotionSnapshot)
                            {
                                var customKey = new CustomKey(nameKey.GlobalIndex, "MyCubeGrid");

                                if (customKeys.TryGetValue(customKey, out int key))
                                    nameKey = new(key);
                                else
                                    customKeys[customKey] = (nameKey = ProfilerKeyCache.GetOrAdd("MyCubeGrid " + nameKey.ToString())).GlobalIndex;
                            }
                        }
                    }
                }

                activeTimer = GetOrAddTimer(nameKey, _event.Depth - prevDepth);

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

            foreach (var item in timers.Values.OrderByDescending(t => t.ElapsedTime))
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

                var eventInfo = new CombinedEventInfo(timer, recording.NumFrames);

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

                foreach (var item in timer.Children.Values.OrderByDescending(t => t.ElapsedTime))
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
            if (_event.DataType == ProfilerEvent.DataTypeOption.ObjectAndCategory)
                return _event.Category;

            if (_event.DataType == ProfilerEvent.DataTypeOption.Object)
            {
                switch (_event.DataObject)
                {
                case CubeGridInfoProxy.MotionSnapshot: return ProfilerEvent.EventCategory.Grids;
                case CubeBlockInfoProxy.Snapshot:      return ProfilerEvent.EventCategory.Blocks;
                case CharacterInfoProxy.Snapshot:      return ProfilerEvent.EventCategory.Characters;
                case FloatingObjectInfoProxy.Snapshot: return ProfilerEvent.EventCategory.FloatingObjects;
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
        var times = new (ProfilerEvent.EventCategory Category, double TotalTime)[(int)ProfilerEvent.EventCategory.CategoryCount];

        for (int i = 0; i < times.Length; i++)
            times[i].Category = (ProfilerEvent.EventCategory)i;

        for (int i = 0; i < mainGroup.Events.Length; i++)
        {
            ref var _event = ref mainGroup.Events[i];

            times[(int)_event.Category].TotalTime += _event.ElapsedTime.TotalMilliseconds;
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
                .AppendFormat("{0:N2}  -  ", t.TotalTime / recording.NumFrames)
                .AppendFormat("{0:N1}", t.TotalTime);

            if (i < times.Length - 1 && (i > times.Length - 2 || times[i + 1].TotalTime != 0))
                sb.AppendLine();
        }

        return sb.ToString();
    }

    class CombinedEventInfo
    {
        public double MillisecondsAveragePerFrame;
        public int CallCount;
        public double MillisecondsMin;
        public double MillisecondsMax;
        public double MillisecondsAverage;
        public double MillisecondsVariance;

        public CombinedEventInfo(AccumTimer timer, int frameCount)
        {
            MillisecondsAveragePerFrame = ProfilerTimer.MillisecondsFromTicks(timer.ElapsedTime) / frameCount;
            CallCount = timer.AccumCount;
            MillisecondsMin = ProfilerTimer.MillisecondsFromTicks(timer.MinElapsedTime);
            MillisecondsMax = ProfilerTimer.MillisecondsFromTicks(timer.MaxElapsedTime);
            MillisecondsAverage = timer.ElapsedTimeM;
            MillisecondsVariance = timer.AccumCount > 1 ? timer.ElapsedTimeS / (timer.AccumCount - 1) : 0;
        }

        public override string ToString()
        {
            return $"""
                    Avg ms/frame: {MillisecondsAveragePerFrame:N3}

                    Call Count: {CallCount}
                    Min ms: {MillisecondsMin:N3}
                    Max ms: {MillisecondsMax:N3}
                    Avg ms: {MillisecondsAverage:N3}
                    StdDev ms: {Math.Sqrt(MillisecondsVariance):N3}
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
    [ProtoMember(1)] public List<RefObjWrapper<Snapshot>> Snapshots = [];
    [ProtoMember(2)] public long EntityId;
    [ProtoMember(3)] public MyCubeSize GridSize;
    [ProtoMember(4)] public bool IsNPC;
    [ProtoMember(5)] public bool IsPreview;

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public CubeGridInfoProxy Grid;
        [ProtoMember(2)] public List<RefObjWrapper<MotionSnapshot>> MotionSnapshots = [];
        [ProtoMember(3)] public ulong FrameIndex;
        [ProtoMember(4)] public bool IsStatic;
        [ProtoMember(5)] public string Name;
        [ProtoMember(6)] public long OwnerId;
        [ProtoMember(7)] public StringId OwnerName;
        [ProtoMember(8)] public int BlockCount;
        [ProtoMember(9)] public Vector3I Size;
        [ProtoMember(10)] public int PCU;
        [ProtoMember(11)] public bool IsPowered;
        [ProtoMember(12)] public int ConnectedGrids;
        [ProtoMember(13)] public int GroupId;
        [ProtoMember(14)] public int GroupSize;

        public Snapshot(CubeGridInfoProxy gridInfo, MyCubeGrid grid, Dictionary<GridGroup, int> gridGroupsToIds)
        {
            Grid = gridInfo;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;
            IsStatic = grid.IsStatic;

            var bigOwners = grid.BigOwners;
            long ownerId = bigOwners != null && bigOwners.Count > 0 ? bigOwners[0] : 0;
            var ownerIdentity = ownerId != 0 ? MySession.Static.Players.TryGetIdentity(ownerId) : null;

            Name = grid.DisplayName;
            OwnerId = ownerId;
            OwnerName = new StringId(ownerIdentity?.DisplayName);
            BlockCount = grid.BlocksCount;
            PCU = grid.BlocksPCU;
            Size = grid.Max - grid.Min + Vector3I.One;
            IsPowered = grid.IsPowered;

            var groupNode = MyCubeGridGroups.Static.Physical.GetNode(grid);

            if (groupNode != null)
            {
                if (!gridGroupsToIds.TryGetValue(groupNode.Group, out GroupId))
                    gridGroupsToIds.Add(groupNode.Group, GroupId = gridGroupsToIds.Count + 1);

                GroupSize = groupNode.Group.Nodes.Count;
                ConnectedGrids = groupNode.LinkCount;
            }
            else
            {
                GroupId = -1;
                GroupSize = 0;
                ConnectedGrids = 0;
            }

            _ = GetMotionSnapshot(grid);
        }

        public Snapshot()
        {
            Grid = null!;
            Name = "";
        }

        public MotionSnapshot GetMotionSnapshot(MyCubeGrid grid)
        {
            var lastSnapshot = MotionSnapshots.Count > 0 ? MotionSnapshots[^1].Object : null;

            if (lastSnapshot != null && lastSnapshot.Equals(grid))
                return lastSnapshot;

            var snapshot = new MotionSnapshot(this, grid);

            MotionSnapshots.Add(new(snapshot));

            return snapshot;
        }

        public bool Equals(MyCubeGrid grid, Dictionary<GridGroup, int> gridGroupsToIds)
        {
            // Grid info is captured at the end of the frame. State changes within the frame are not seen.
            if (MySandboxGame.Static.SimulationFrameCounter == FrameIndex)
                return true;

            var bigOwners = grid.BigOwners;
            long ownerId = bigOwners != null && bigOwners.Count > 0 ? bigOwners[0] : 0;
            var groupNode = MyCubeGridGroups.Static.Physical.GetNode(grid);

            return IsStatic == grid.IsStatic
                && Name == grid.DisplayName
                && OwnerId == ownerId
                && BlockCount == grid.BlocksCount
                && PCU == grid.BlocksPCU
                && Size == (grid.Max - grid.Min + Vector3I.One)
                && IsPowered == grid.IsPowered
                && ConnectedGrids == (groupNode?.LinkCount ?? 0)
                && GroupId == (groupNode != null ? gridGroupsToIds.GetValueOrDefault(groupNode.Group, -1) : -1)
                && GroupSize == (groupNode?.Group.Nodes.Count ?? 0);
        }

        public bool Equals(Snapshot other)
        {
            return IsStatic == other.IsStatic
                && Name == other.Name
                && OwnerId == other.OwnerId
                && OwnerName == other.OwnerName
                && BlockCount == other.BlockCount
                && Size == other.Size
                && PCU == other.PCU
                && IsPowered == other.IsPowered
                && ConnectedGrids == other.ConnectedGrids
                && GroupId == other.GroupId
                && GroupSize == other.GroupSize;
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

    [ProtoContract]
    public class MotionSnapshot
    {
        [ProtoMember(1, AsReference = true)] public Snapshot BaseSnapshot;
        [ProtoMember(2)] public ulong FrameIndex;
        [ProtoMember(3)] public Vector3D Position;
        [ProtoMember(4)] public float Speed;
        [ProtoMember(5)] public int PhysicsCluster;

        public MotionSnapshot()
        {
            BaseSnapshot = null!;
            PhysicsCluster = -1;
        }

        public MotionSnapshot(Snapshot baseSnapshot, MyCubeGrid grid)
        {
            BaseSnapshot = baseSnapshot;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;
            Position = grid.PositionComp.GetPosition();
            Speed = grid.LinearVelocity.Length();
            PhysicsCluster = PhysicsHelper.GetClusterIdForObject(grid.Physics);
        }

        public bool Equals(MyCubeGrid grid)
        {
            // Grid info is captured at the end of the frame. State changes within the frame are not seen.
            if (MySandboxGame.Static.SimulationFrameCounter == FrameIndex)
                return true;

            return Vector3D.DistanceSquared(Position, grid.PositionComp.GetPosition()) < (0.1 * 0.1)
                && Math.Abs(Speed - grid.LinearVelocity.Length()) < 0.1
                && PhysicsCluster == PhysicsHelper.GetClusterIdForObject(grid.Physics);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var b = BaseSnapshot;

            if (b.IsStatic && b.Grid.GridSize == MyCubeSize.Large)
                sb.Append("Static");
            else
                sb.Append(b.Grid.GridSize);

            sb.AppendLine($" Grid, ID: {b.Grid.EntityId}");

            if (b.Grid.IsNPC)
                sb.AppendLine("    Is NPC: True");

            if (b.Grid.IsPreview)
                sb.AppendLine($"    Is Preview: {b.Grid.IsPreview}");

            var idPart = b.OwnerName.String != null ? $", ID: " : null;

            sb.AppendLine($"    Name: {b.Name}");
            sb.AppendLine($"    Owner: {b.OwnerName}{idPart}{b.OwnerId}");
            sb.AppendLine($"    Blocks: {b.BlockCount}");
            sb.AppendLine($"    PCU: {b.PCU}");
            sb.AppendLine($"    Size: {b.Size}");
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

            sb.AppendLine($"    Is Powered: {b.IsPowered}");

            if (b.GroupSize > 1)
            {
                sb.AppendLine($"    Group ID: {b.GroupId}");
                sb.AppendLine($"    Group Size: {b.GroupSize}");
                sb.AppendLine($"    Connected Grid Count: {b.ConnectedGrids}");
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
    [ProtoMember(1)] public List<RefObjWrapper<Snapshot>> Snapshots = [];
    [ProtoMember(2)] public long EntityId;
    [ProtoMember(3)] public TypeProxy BlockType;

    [ProtoContract]
    public class Snapshot
    {
        [ProtoMember(1, AsReference = true)] public CubeGridInfoProxy.Snapshot GridSnapshot;
        [ProtoMember(2, AsReference = true)] public CubeBlockInfoProxy Block;
        [ProtoMember(3)] public ulong FrameIndex;
        [ProtoMember(4)] public string? CustomName;
        [ProtoMember(5)] public long OwnerId;
        [ProtoMember(6)] public StringId OwnerName;
        [ProtoMember(7)] public Vector3I LocalPosition;

        public Vector3D LastWorldPosition
        {
            get
            {
                var gridMotion = GridSnapshot.MotionSnapshots[^1].Object;
                float blockSize = GridSnapshot.Grid.GridSize == MyCubeSize.Large ? 2.5f : 0.5f;

                return gridMotion.Position + LocalPosition * blockSize;
            }
        }

        public Snapshot(CubeGridInfoProxy.Snapshot gridInfo, CubeBlockInfoProxy blockInfo, MyCubeBlock block)
        {
            GridSnapshot = gridInfo;
            Block = blockInfo;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;

            long ownerId = block.OwnerId;
            var ownerIdentity = ownerId != 0 ? MySession.Static.Players.TryGetIdentity(ownerId) : null;

            CustomName = (block as MyTerminalBlock)?.CustomName.ToString();
            OwnerId = ownerId;
            OwnerName = new StringId(ownerIdentity?.DisplayName);
            LocalPosition = block.Min;
        }

        public Snapshot()
        {
            GridSnapshot = null!;
            Block = null!;
        }

        public bool Equals(MyCubeBlock block)
        {
            // Grid info is captured at the end of the frame. State changes within the frame are not seen.
            if (MySandboxGame.Static.SimulationFrameCounter == FrameIndex)
                return true;

            return GridSnapshot.Grid.EntityId == block.CubeGrid.EntityId
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
                   Last Position: {Vector3D.Round(LastWorldPosition, 1)}
                {GridSnapshot}
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
    [ProtoMember(1)] public List<RefObjWrapper<Snapshot>> Snapshots = [];
    [ProtoMember(2)] public long EntityId;
    [ProtoMember(3)] public long IdentityId;
    [ProtoMember(4)] public ulong PlatformId;
    [ProtoMember(5)] public string Name;

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
    [ProtoMember(1)] public List<RefObjWrapper<Snapshot>> Snapshots = [];
    [ProtoMember(2)] public long EntityId;
    [ProtoMember(3)] public StringId ItemTypeId;
    [ProtoMember(4)] public StringId ItemSubtypeId;

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
