using System;
using System.Collections.Generic;
using System.Linq;
using Havok;
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
            data.Object = data.Object?.ToString();
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

    public static RecordingAnalysisInfo AnalyzeRecording(Profiler.EventsRecording recording)
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
}

class PhysicsClusterInfoProxy
{
    public int ID;
    public List<Snapshot> Snapshots = [];

    public class Snapshot
    {
        public PhysicsClusterInfoProxy Cluster;
        public BoundingBoxD AABB;
        public bool HasWorld;
        public int RigidBodyCount;
        public int ActiveRigidBodyCount;
        public int CharacterCount;

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

    public Snapshot GetSnapshot(MyClusterTree.MyCluster cluster)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1] : null;

        if (lastSnapshot != null && lastSnapshot.Equals(cluster))
            return lastSnapshot;

        var snapshot = new Snapshot(this, cluster);

        Snapshots.Add(snapshot);

        return snapshot;
    }
}

class CubeGridInfoProxy
{
    public long EntityId;
    public MyCubeSize GridSize;
    public List<Snapshot> Snapshots = [];

    public class Snapshot
    {
        public CubeGridInfoProxy Grid;
        public ulong FrameIndex;
        public string CustomName;
        public long OwnerId;
        public string? OwnerName;
        public int BlockCount;
        public Vector3D Position;
        // TODO: Add Speed

        public Snapshot(CubeGridInfoProxy gridInfo, MyCubeGrid grid)
        {
            Grid = gridInfo;
            FrameIndex = MySandboxGame.Static.SimulationFrameCounter;

            long ownerId = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            CustomName = grid.DisplayName;
            OwnerId = ownerId;
            OwnerName = ownerIdentity?.DisplayName;
            BlockCount = grid.BlocksCount;
            Position = grid.PositionComp.GetPosition();
        }

        public bool Equals(MyCubeGrid grid)
        {
            long ownerId = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
            var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);

            return CustomName == grid.DisplayName
                && OwnerId == ownerId
                && OwnerName == ownerIdentity?.DisplayName
                && BlockCount == grid.BlocksCount
                && Vector3D.Round(Position, 1) == Vector3D.Round(grid.PositionComp.GetPosition(), 1);
        }

        public override string ToString()
        {
            var idPart = OwnerName != null ? $", ID: " : null;

            return $"""
                {Grid.GridSize} Grid, ID: {Grid.EntityId}
                   Custom Name: {CustomName}
                   Owner: {OwnerName}{idPart}{OwnerId}
                   Blocks: {BlockCount}
                   Position: {Vector3D.Round(Position, 0)}
                """;
        }
    }

    public CubeGridInfoProxy(MyCubeGrid grid)
    {
        EntityId = grid.EntityId;
        GridSize = grid.GridSizeEnum;
    }

    public Snapshot GetSnapshot(MyCubeGrid grid)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1] : null;

        if (lastSnapshot != null && lastSnapshot.Equals(grid))
            return lastSnapshot;

        var snapshot = new Snapshot(this, grid);

        Snapshots.Add(snapshot);

        return snapshot;
    }
}

class CubeBlockInfoProxy
{
    public long EntityId;
    public Type BlockType;
    public List<Snapshot> Snapshots = [];

    public class Snapshot
    {
        public CubeGridInfoProxy.Snapshot Grid;
        public CubeBlockInfoProxy Block;
        public ulong FrameIndex;
        public string? CustomName;
        public long OwnerId;
        public string? OwnerName;
        public Vector3D Position;

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
                {Block.BlockType.Name}, ID: {Block.EntityId}
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
        BlockType = block.GetType();
    }

    public Snapshot GetSnapshot(CubeGridInfoProxy.Snapshot gridInfo, MyCubeBlock block)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1] : null;

        if (lastSnapshot != null && lastSnapshot.Equals(block))
            return lastSnapshot;

        var snapshot = new Snapshot(gridInfo, this, block);

        Snapshots.Add(snapshot);

        return snapshot;
    }
}

class CharacterInfoProxy
{
    public long EntityId;
    public long IdentityId;
    public ulong PlatformId;
    public string Name;
    public List<Snapshot> Snapshots = [];

    public class Snapshot
    {
        public CharacterInfoProxy Character;
        public Vector3D Position;

        public Snapshot(CharacterInfoProxy characterInfo, MyCharacter character)
        {
            Character = characterInfo;
            Position = character.PositionComp.GetPosition();
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

    public Snapshot GetSnapshot(MyCharacter character)
    {
        var lastSnapshot = Snapshots.Count > 0 ? Snapshots[^1] : null;

        if (lastSnapshot != null && lastSnapshot.Equals(character))
            return lastSnapshot;

        var snapshot = new Snapshot(this, character);

        Snapshots.Add(snapshot);

        return snapshot;
    }
}

class VoxelInfoProxy
{
    public long EntityId;
    public string Name;
    public BoundingBoxD AABB;

    public VoxelInfoProxy(MyVoxelBase voxel)
    {
        EntityId = voxel.EntityId;
        Name = voxel.Name;
        AABB = voxel.PositionComp.WorldAABB;
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
        public int MinObjects;
        public int MaxObjects;
        public int MinActiveObjects;
        public int MaxActiveObjects;
        public int MinCharacters;
        public int MaxCharacters;

        public double TotalTime;
        public double AverageTimePerFrame;
        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public Builder(PhysicsClusterInfoProxy.Snapshot info)
        {
            ID = info.Cluster.ID;
            MinObjects = info.RigidBodyCount;
            MaxObjects = info.RigidBodyCount;
            MinActiveObjects = info.ActiveRigidBodyCount;
            MaxActiveObjects = info.ActiveRigidBodyCount;
            MinCharacters = info.CharacterCount;
            MaxCharacters = info.CharacterCount;

            AABBs.Add(info.AABB);
        }

        public void Add(PhysicsClusterInfoProxy.Snapshot info)
        {
            AABBs.Add(info.AABB);

            if (info.HasWorld)
            {
                MinObjects = Math.Min(MinObjects, info.RigidBodyCount);
                MaxObjects = Math.Max(MaxObjects, info.RigidBodyCount);
                MinActiveObjects = Math.Min(MinActiveObjects, info.ActiveRigidBodyCount);
                MaxActiveObjects = Math.Max(MaxActiveObjects, info.ActiveRigidBodyCount);
                MinCharacters = Math.Min(MinCharacters, info.CharacterCount);
                MaxCharacters = Math.Max(MaxCharacters, info.CharacterCount);
            }
        }

        public PhysicsClusterAnalysisInfo Finish()
        {
            return new PhysicsClusterAnalysisInfo(ID, AABBs.ToArray(),
                MinObjects, MaxObjects, MinActiveObjects, MaxActiveObjects, MinCharacters, MaxCharacters,
                TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public int ID;
    public BoundingBoxD[] AABBs;
    public int MinObjects;
    public int MaxObjects;
    public int MinActiveObjects;
    public int MaxActiveObjects;
    public int MinCharacters;
    public int MaxCharacters;

    public double TotalTime;
    public double AverageTimePerFrame;
    public int IncludedInNumGroups;
    public int NumFramesCounted;

    public PhysicsClusterAnalysisInfo(
        int id, BoundingBoxD[] aabbs,
        int minObjects, int maxObjects,
        int minActiveObjects, int maxActiveObjects,
        int minCharacters, int maxCharacters,
        double totalTime, double averageTimePerFrame,
        int includedInNumGroups, int numFramesCounted)
    {
        ID = id;
        AABBs = aabbs;
        MinObjects = minObjects;
        MaxObjects = maxObjects;
        MinActiveObjects = minActiveObjects;
        MaxActiveObjects = maxActiveObjects;
        MinCharacters = minCharacters;
        MaxCharacters = maxCharacters;
        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumGroups = includedInNumGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        return $"""
                Physics Cluster, ID: {ID}
                    {(AABBs.Length == 1
                        ? ToString(Round(AABBs[0]))
                        : $"AABBs: {string.Join(", ", AABBs.Select(Round).Distinct().Select(ToString2))}")}
                    Num Objects{(MinObjects == MaxObjects ? $": {MaxObjects}" : $", Min: {MinObjects}, Max: {MaxObjects}")}
                    Num Active Objects{(MinActiveObjects == MaxActiveObjects ? $": {MaxActiveObjects}" : $", Min: {MinActiveObjects}, Max: {MaxActiveObjects}")}
                    Num Characters{(MinCharacters == MaxCharacters ? $": {MaxCharacters}" : $", Min: {MinCharacters}, Max: {MaxCharacters}")}
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
        public HashSet<string> CustomNames = [];
        public Dictionary<long, string?> Owners = [];
        public HashSet<int> BlockCounts = [];
        public HashSet<Vector3D> Positions = [];
        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime;
        public double AverageTimePerFrame;

        public Builder(CubeGridInfoProxy.Snapshot info)
        {
            EntityId = info.Grid.EntityId;
            GridSize = info.Grid.GridSize;

            Add(info);
        }

        public void Add(CubeGridInfoProxy.Snapshot info)
        {
            CustomNames.Add(info.CustomName);
            Owners[info.OwnerId] = info.OwnerName;
            BlockCounts.Add(info.BlockCount);
            Positions.Add(info.Position);
        }

        public CubeGridAnalysisInfo Finish()
        {
            return new CubeGridAnalysisInfo(EntityId, GridSize, CustomNames.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                BlockCounts.ToArray(), Positions.ToArray(), TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public long EntityId;
    public MyCubeSize GridSize;
    public string[] CustomNames;
    public (long ID, string? Name)[] Owners;
    public int[] BlockCounts;
    public Vector3D[] Positions;

    public double TotalTime;
    public double AverageTimePerFrame;
    public int IncludedInNumGroups;
    public int NumFramesCounted;

    public CubeGridAnalysisInfo(
        long entityId, MyCubeSize gridSize,
        string[] customNames, (long ID, string? Name)[] owners,
        int[] blockCounts, Vector3D[] positions,
        double totalTime, double averageTimePerFrame,
        int includedInNumGroups, int numFramesCounted)
    {
        EntityId = entityId;
        GridSize = gridSize;
        CustomNames = customNames;
        Owners = owners;
        BlockCounts = blockCounts;
        Positions = positions;
        TotalTime = totalTime;
        AverageTimePerFrame = averageTimePerFrame;
        IncludedInNumGroups = includedInNumGroups;
        NumFramesCounted = numFramesCounted;
    }

    public override string ToString()
    {
        return $"""
                {GridSize} Grid, ID: {EntityId}
                    {(CustomNames.Length == 1 ? $"Custom Name: {CustomNames[0]}" : $"Custom Names: {string.Join(", ", CustomNames)}")}
                    Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", ID: " : null) + o.ID.ToString()})"))}
                    Block Count{(BlockCounts.Length > 1 ? "s" : "")}: {string.Join(", ", BlockCounts)}
                    Position{(Positions.Length > 1 ? "s" : "")}: {string.Join(", ", Positions.Select(p => Vector3D.Round(p, 0)).Distinct().Select(p => $"({p})"))}
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
        public long GridId;
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
            GridId = info.Grid.Grid.EntityId;
            BlockType = info.Block.BlockType.Type;

            Add(info);
        }

        public void Add(CubeBlockInfoProxy.Snapshot info)
        {
            if (info.CustomName != null)
                CustomNames.Add(info.CustomName);

            Owners[info.OwnerId] = info.OwnerName;
            Positions.Add(info.Position);
        }

        public CubeBlockAnalysisInfo Finish()
        {
            return new CubeBlockAnalysisInfo(EntityId, GridId, BlockType, CustomNames.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                Positions.ToArray(), TotalTime, AverageTimePerFrame, IncludedInGroups.Count, FramesCounted.Count);
        }
    }

    public long EntityId;
    public long GridId;
    public Type BlockType;
    public string[] CustomNames;
    public (long ID, string? Name)[] Owners;
    public Vector3D[] Positions;

    public double TotalTime;
    public double AverageTimePerFrame;
    public int IncludedInNumGroups;
    public int NumFramesCounted;

    public CubeBlockAnalysisInfo(
        long entityId, long gridId, Type blockType,
        string[] customNames, (long ID, string? Name)[] owners,
        Vector3D[] positions,
        double totalTime, double averageTimePerFrame,
        int includedInNumGroups, int numFramesCounted)
    {
        EntityId = entityId;
        GridId = gridId;
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
                    Grid ID: {GridId}
                    {(CustomNames.Length == 1 ? $"Custom Name: {CustomNames[0]}" : $"Custom Names: {string.Join(", ", CustomNames)}")}
                    Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", ID: " : null) + o.ID.ToString()})"))}
                    Position{(Positions.Length > 1 ? "s" : "")}: {string.Join(", ", Positions.Select(p => Vector3D.Round(p, 0)).Distinct().Select(p => $"({p})"))}
                Total Time: {TotalTime:N1}ms
                Average Time: {AverageTimePerFrame:N2}ms
                Counted Frames: {NumFramesCounted}{(IncludedInNumGroups > 1 ? $"\r\nProcessed over {IncludedInNumGroups} threads" : "")}
                """;
    }
}
