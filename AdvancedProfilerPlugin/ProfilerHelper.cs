using System;
using System.Collections.Generic;
using System.Linq;
using Havok;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Replication;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Network;
using VRageMath;
using VRageMath.Spatial;

namespace AdvancedProfiler;

static class ProfilerHelper
{
    internal static readonly ResolveProfilerEventObjectDelegate ProfilerEventObjectResolver = ResolveProfilerEventObject;

    static void ResolveProfilerEventObject(Dictionary<object, object> cache, ref ProfilerEvent _event)
    {
        switch (_event.ExtraValue.Object)
        {
        case Type type:
            {
                _event.ExtraValue.Format = "Type: {0}";

                if (cache.TryGetValue(type, out var cachedObj))
                {
                    _event.ExtraValue.Object = (string)cachedObj;
                    break;
                }

                cache[type] = _event.ExtraValue.Object = type.FullName!;
            }
            break;
        case MyClusterTree.MyCluster cluster:
            {
                _event.ExtraValue.Format = "{0}";

                if (cache.TryGetValue(cluster, out var cachedObj))
                {
                    _event.ExtraValue.Object = (PhysicsClusterInfoProxy)cachedObj;
                    break;
                }

                cache[cluster] = _event.ExtraValue.Object = new PhysicsClusterInfoProxy(cluster);
            }
            break;
        case MyCubeGrid grid:
            {
                _event.ExtraValue.Format = "{0}";

                if (cache.TryGetValue(grid, out var cachedObj))
                {
                    _event.ExtraValue.Object = (CubeGridInfoProxy)cachedObj;
                    break;
                }

                cache[grid] = _event.ExtraValue.Object = new CubeGridInfoProxy(grid);
            }
            break;
        case MyCubeBlock block:
            {
                _event.ExtraValue.Format = "{0}";

                if (cache.TryGetValue(block, out var cachedObj))
                {
                    _event.ExtraValue.Object = (CubeBlockInfoProxy)cachedObj;
                    break;
                }

                var grid = block.CubeGrid;

                if (!cache.TryGetValue(grid, out cachedObj) || cachedObj is not CubeGridInfoProxy cachedGridProxy)
                    cache[grid] = cachedGridProxy = new CubeGridInfoProxy(grid);

                cache[block] = _event.ExtraValue.Object = new CubeBlockInfoProxy(block, cachedGridProxy);
            }
            break;
        case MyExternalReplicable<MyCharacter> charRepl:
            {
                _event.ExtraValue.Format = "{0}";

                var character = charRepl.Instance;

                if (character != null)
                {
                    if (cache.TryGetValue(character, out var cachedObj))
                    {
                        _event.ExtraValue.Object = (CharacterInfoProxy)cachedObj;
                        break;
                    }

                    cache[character] = _event.ExtraValue.Object = new CharacterInfoProxy(character);
                }
                else
                {
                    _event.ExtraValue.Object = null;
                    _event.ExtraValue.Format = "Empty character replicable{0}";
                }
            }
            break;
        case MyExternalReplicable<MyCubeGrid> gridRepl:
            {
                _event.ExtraValue.Format = "{0}";

                var grid = gridRepl.Instance;

                if (grid != null)
                {
                    if (cache.TryGetValue(grid, out var cachedObj))
                    {
                        _event.ExtraValue.Object = (CubeGridInfoProxy)cachedObj;
                        break;
                    }

                    cache[grid] = _event.ExtraValue.Object = new CubeGridInfoProxy(grid);
                }
                else
                {
                    _event.ExtraValue.Object = null;
                    _event.ExtraValue.Format = "Empty cube grid replicable{0}";
                }
            }
            break;
        case MyExternalReplicable<MySyncedBlock> blockRepl:
            {
                _event.ExtraValue.Format = "{0}";

                var block = blockRepl.Instance;

                if (block != null)
                {
                    if (cache.TryGetValue(block, out var cachedObj))
                    {
                        _event.ExtraValue.Object = (CubeBlockInfoProxy)cachedObj;
                        break;
                    }

                    var grid = block.CubeGrid;

                    if (!cache.TryGetValue(grid, out cachedObj) || cachedObj is not CubeGridInfoProxy cachedGridProxy)
                        cache[grid] = cachedGridProxy = new CubeGridInfoProxy(grid);

                    cache[block] = _event.ExtraValue.Object = new CubeBlockInfoProxy(block, cachedGridProxy);
                }
                else
                {
                    _event.ExtraValue.Object = null;
                    _event.ExtraValue.Format = "Empty cube block replicable{0}";
                }
            }
            break;
        case MyExternalReplicable<MyVoxelBase> voxelRepl:
            {
                _event.ExtraValue.Format = "{0}";

                var voxel = voxelRepl.Instance;

                if (voxel != null)
                {
                    if (cache.TryGetValue(voxel, out var cachedObj))
                    {
                        _event.ExtraValue.Object = (VoxelInfoProxy)cachedObj;
                        break;
                    }

                    cache[voxel] = _event.ExtraValue.Object = new VoxelInfoProxy(voxel);
                }
                else
                {
                    _event.ExtraValue.Object = null;
                    _event.ExtraValue.Object = "Empty voxel replicable{0}";
                }
            }
            break;
        case IMyReplicable replicable:
            {
                _event.ExtraValue.Object = null;
                _event.ExtraValue.Format = replicable.GetType().Name;
            }
            break;
        default:
            break;
        }
    }

    public static RecordingAnalysisInfo AnalyzeRecording(Profiler.EventsRecording recording)
    {
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

            int frameEndOffset = 0;

            if (events.FrameEndEventIndices[0] < events.FrameStartEventIndices[0])
                frameEndOffset = 1;

            for (int f = 0; f < events.FrameStartEventIndices.Length; f++)
            {
                if (frameEndOffset + f >= events.FrameEndEventIndices.Length)
                    break;

                int startEventIndex = events.FrameStartEventIndices[f];
                int endEventIndex = events.FrameEndEventIndices[frameEndOffset + f];
                int startSegmentIndex = startEventIndex / events.SegmentSize;
                int endSegmentIndex = endEventIndex / events.SegmentSize;

                for (int s = startSegmentIndex; s <= endSegmentIndex; s++)
                {
                    var segment = events.EventSegments[s];
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
                                case PhysicsClusterInfoProxy clusterInfo:
                                    AnalyzePhysicsCluster(clusterInfo, in _event, groupId, f);
                                    break;
                                case CubeGridInfoProxy gridInfo:
                                    AnalyzeGrid(gridInfo, in _event, groupId, f);
                                    break;
                                case CubeBlockInfoProxy blockInfo:
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

        foreach (var item in clusters)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        foreach (var item in grids)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        foreach (var item in progBlocks)
            item.Value.AverageTimePerFrame = item.Value.TotalTime / item.Value.FramesCounted.Count;

        return new RecordingAnalysisInfo(
            clusters.Values.Select(c => c.Finish()).ToArray(),
            grids.Values.Select(c => c.Finish()).ToArray(),
            progBlocks.Values.Select(c => c.Finish()).ToArray());

        void AnalyzePhysicsCluster(PhysicsClusterInfoProxy clusterInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (clusters.TryGetValue(clusterInfo.ID, out var anInf))
                anInf.Add(clusterInfo);
            else
                clusters.Add(clusterInfo.ID, anInf = new(clusterInfo));

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

        void AnalyzeGrid(CubeGridInfoProxy gridInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (grids.TryGetValue(gridInfo.EntityId, out var anInf))
                anInf.Add(gridInfo);
            else
                grids.Add(gridInfo.EntityId, anInf = new(gridInfo));

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

        void AnalyzeBlock(CubeBlockInfoProxy blockInfo, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (blockInfo.BlockType != typeof(Sandbox.Game.Entities.Blocks.MyProgrammableBlock))
                return;

            if (progBlocks.TryGetValue(blockInfo.EntityId, out var anInf))
                anInf.Add(blockInfo);
            else
                progBlocks.Add(blockInfo.EntityId, anInf = new(blockInfo));

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
    public BoundingBoxD AABB;
    public bool HasWorld;
    public int RigidBodyCount;
    public int ActiveRigidBodyCount;
    public int CharacterCount;

    public PhysicsClusterInfoProxy(MyClusterTree.MyCluster cluster)
    {
        var hkWorld = cluster.UserData as HkWorld;

        ID = cluster.ClusterId;
        AABB = cluster.AABB;

        if (hkWorld != null)
        {
            HasWorld = true;
            RigidBodyCount = hkWorld.RigidBodies.Count;
            ActiveRigidBodyCount = hkWorld.ActiveRigidBodies.Count;
            CharacterCount = hkWorld.CharacterRigidBodies.Count;
        }
    }

    public override string ToString()
    {
        if (!HasWorld)
            return "Physics Cluster without HKWorld";

        return $"""
                Physics Cluster, ID: {ID}
                   Center: {Vector3D.Round(AABB.Center, 0)}
                   Size: {Vector3D.Round(AABB.Size, 0)}
                   Rigid Bodies: {RigidBodyCount} (Active: {ActiveRigidBodyCount})
                   Characters: {CharacterCount}
                """;
    }
}

class CubeGridInfoProxy
{
    public long EntityId;
    public MyCubeSize GridSize;
    public string CustomName;
    public long OwnerId;
    public string? OwnerName;
    public int BlockCount;
    public Vector3D Position;

    public CubeGridInfoProxy(MyCubeGrid grid)
    {
        long ownerId = 0;

        if (grid.BigOwners.Count > 0)
            ownerId = grid.BigOwners[0];

        var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);
        //var ownerFaction = MySession.Static.Factions.GetPlayerFaction(ownerId);

        EntityId = grid.EntityId;
        GridSize = grid.GridSizeEnum;
        CustomName = grid.DisplayName;
        OwnerId = ownerId;
        OwnerName = ownerIdentity?.DisplayName;
        BlockCount = grid.BlocksCount;
        Position = grid.PositionComp.GetPosition();
    }

    public override string ToString()
    {
        var idPart = OwnerName != null ? $", Id: " : null;

        return $"""
                {GridSize} Grid, ID: {EntityId}
                   Custom Name: {CustomName}
                   Owner: {OwnerName}{idPart}{OwnerId}
                   Blocks: {BlockCount}
                   Position: {Vector3D.Round(Position, 0)}
                """;
    }
}

class CubeBlockInfoProxy
{
    public long EntityId;
    public CubeGridInfoProxy Grid;
    public string? CustomName;
    public long OwnerId;
    public string? OwnerName;
    public Type BlockType;
    public Vector3D Position;

    public CubeBlockInfoProxy(MyCubeBlock block, CubeGridInfoProxy gridInfo)
    {
        long ownerId = block.OwnerId;
        var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);
        //var ownerFaction = MySession.Static.Factions.GetPlayerFaction(ownerId);

        EntityId = block.EntityId;
        Grid = gridInfo;
        CustomName = (block as MyTerminalBlock)?.CustomName.ToString();
        OwnerId = ownerId;
        OwnerName = ownerIdentity?.DisplayName;
        BlockType = block.GetType();
        Position = block.PositionComp.GetPosition();
    }

    public override string ToString()
    {
        var idPart = OwnerName != null ? $", Id: " : null;

        return $"""
                Block, ID: {EntityId}
                   Custom Name: {CustomName}
                   Owner: {OwnerName}{idPart}{OwnerId}
                   Type: {BlockType.Name}
                   Position: {Vector3D.Round(Position, 1)}
                {Grid}
                """;
    }
}

class CharacterInfoProxy
{
    public long EntityId;
    public long IdentityId;
    public ulong PlatformId;
    public string Name;
    public Vector3D Position;

    public CharacterInfoProxy(MyCharacter character)
    {
        EntityId = character.EntityId;

        var identity = character.GetIdentity();
        IdentityId = identity?.IdentityId ?? 0;
        PlatformId = character.ControlSteamId;
        Name = identity?.DisplayName ?? "";
        Position = character.PositionComp.GetPosition();
    }

    public override string ToString()
    {
        return $"""
                Character, ID: {EntityId}
                   Identity ID: {IdentityId}
                   Platform ID: {PlatformId}
                   Name: {Name}
                   Position: {Vector3D.Round(Position, 1)}
                """;
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
    public PhysicsClusterAnalysisInfo[] PhysicsClusters;
    public CubeGridAnalysisInfo[] Grids;
    public CubeBlockAnalysisInfo[] ProgrammableBlocks;

    public RecordingAnalysisInfo(PhysicsClusterAnalysisInfo[] physicsClusters, CubeGridAnalysisInfo[] grids, CubeBlockAnalysisInfo[] programmableBlocks)
    {
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

        public Builder(PhysicsClusterInfoProxy info)
        {
            ID = info.ID;
            MinObjects = info.RigidBodyCount;
            MaxObjects = info.RigidBodyCount;
            MinActiveObjects = info.ActiveRigidBodyCount;
            MaxActiveObjects = info.ActiveRigidBodyCount;
            MinCharacters = info.CharacterCount;
            MaxCharacters = info.CharacterCount;

            AABBs.Add(info.AABB);
        }

        public void Add(PhysicsClusterInfoProxy info)
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
                AABB{(AABBs.Length > 1 ? "s" : "")}: {string.Join(", ", AABBs.Select(b => Round(b)).Distinct().Select(b => $"({ToString(b)})"))}
                Num Objects{(MinObjects == MaxObjects ? $": {MaxObjects}" : $", Min: {MinObjects}, Max: {MaxObjects}")}
                Num Active Objects{(MinActiveObjects == MaxActiveObjects ? $": {MaxActiveObjects}" : $", Min: {MinActiveObjects}, Max: {MaxActiveObjects}")}
                Num Characters{(MinCharacters == MaxCharacters ? $": {MaxCharacters}" : $", Min: {MinCharacters}, Max: {MaxCharacters}")}
                Total Time: {TotalTime:N1}ms
                Average Time: {AverageTimePerFrame:N2}ms
                Counted Frames: {NumFramesCounted}{(IncludedInNumGroups > 1 ? $"\r\nProcessed over {IncludedInNumGroups} threads" : "")}
                """;

        static BoundingBoxD Round(in BoundingBoxD box) => new BoundingBoxD(Vector3D.Round(box.Min, 0), Vector3D.Round(box.Max, 0));

        static string ToString(in BoundingBoxD box) => $"Center:{{{box.Center}}}, Size:{{{box.Size}}}";
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

        public Builder(CubeGridInfoProxy info)
        {
            EntityId = info.EntityId;
            GridSize = info.GridSize;

            Add(info);
        }

        public void Add(CubeGridInfoProxy info)
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
    public (long Id, string? Name)[] Owners;
    public int[] BlockCounts;
    public Vector3D[] Positions;

    public double TotalTime;
    public double AverageTimePerFrame;
    public int IncludedInNumGroups;
    public int NumFramesCounted;

    public CubeGridAnalysisInfo(
        long entityId, MyCubeSize gridSize,
        string[] customNames, (long Id, string? Name)[] owners,
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
                Custom Name{(CustomNames.Length > 1 ? "s" : "")}: {string.Join(", ", CustomNames)}
                Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", Id: " : null) + o.Id.ToString()})"))}
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

        public Builder(CubeBlockInfoProxy info)
        {
            EntityId = info.EntityId;
            GridId = info.Grid.EntityId;
            BlockType = info.BlockType;

            Add(info);
        }

        public void Add(CubeBlockInfoProxy info)
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
    public (long Id, string? Name)[] Owners;
    public Vector3D[] Positions;

    public double TotalTime;
    public double AverageTimePerFrame;
    public int IncludedInNumGroups;
    public int NumFramesCounted;

    public CubeBlockAnalysisInfo(
        long entityId, long gridId, Type blockType,
        string[] customNames, (long Id, string? Name)[] owners,
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
                Custom Name{(CustomNames.Length > 1 ? "s" : "")}: {string.Join(", ", CustomNames)}
                Owner{(Owners.Length > 1 ? "s" : "")}: {string.Join(", ", Owners.Select(o => $"({o.Name}{(o.Name != null ? $", Id: " : null) + o.Id.ToString()})"))}
                Position{(Positions.Length > 1 ? "s" : "")}: {string.Join(", ", Positions.Select(p => Vector3D.Round(p, 0)).Distinct().Select(p => $"({p})"))}
                Total Time: {TotalTime:N1}ms
                Average Time: {AverageTimePerFrame:N2}ms
                Counted Frames: {NumFramesCounted}{(IncludedInNumGroups > 1 ? $"\r\nProcessed over {IncludedInNumGroups} threads" : "")}
                """;
    }
}
