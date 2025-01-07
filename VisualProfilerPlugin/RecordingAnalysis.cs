using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRageMath;

namespace VisualProfiler;

static class RecordingAnalysis
{
    public static RecordingAnalysisInfo AnalyzeRecording(ProfilerEventsRecording recording)
    {
        int numFrames = recording.NumFrames;
        var frameTimes = new long[numFrames];
        var clusters = new Dictionary<int, PhysicsClusterAnalysisInfo.Builder>();
        var grids = new Dictionary<long, CubeGridAnalysisInfo.Builder>();
        var allBlocks = new Dictionary<long, CubeBlockAnalysisInfo.Builder>();
        var progBlocks = new Dictionary<long, CubeBlockAnalysisInfo.Builder>();
        var blockTimesByType = new Dictionary<Type, long>();
        var floatingObjs = new HashSet<long>();

        foreach (var (groupId, group) in recording.Groups)
        {
            if (group.NumRecordedFrames == 0)
                continue;

            for (int f = 0; f < group.NumRecordedFrames; f++)
            {
                var events = group.GetEventsForFrame(f);

                if (events.Length == 0)
                    continue;

                {
                    long startTime = events[0].StartTime;
                    long endTime = events[^1].EndTime;
                    long frameTime = endTime - startTime;

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
                            case CubeGridInfoProxy.MotionSnapshot gridInfo:
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

        if (numFrames > 0)
        {
            frameTimeInfo.Mean /= numFrames;
            frameTimeInfo.StdDev = Math.Sqrt(frameTimeInfo.StdDev / numFrames - frameTimeInfo.Mean * frameTimeInfo.Mean);
        }
        else
        {
            frameTimeInfo.Min = 0;
            frameTimeInfo.StdDev = 0;
        }

        foreach (var (_, item) in clusters)
            item.AverageTimePerFrame = item.TotalTime / item.FramesCounted.Count;

        foreach (var (_, item) in grids)
            item.AverageTimePerFrame = item.TotalTime / item.FramesCounted.Count;

        foreach (var (_, item) in allBlocks)
            item.AverageTimePerFrame = item.TotalTime / item.FramesCounted.Count;

        return new RecordingAnalysisInfo(frameTimeInfo,
            clusters.Values.Select(c => c.Finish()).ToArray(),
            grids.Values.Select(c => c.Finish()).ToArray(),
            allBlocks.Values.Select(c => c.Finish()).ToArray(),
            progBlocks.Values.Select(c => c.Finish()).ToArray(),
            blockTimesByType.OrderByDescending(p => p.Value).Select(p => new KeyValuePair<Type, double>(
                p.Key, ProfilerTimer.MillisecondsFromTicks(p.Value))).ToDictionary(p => p.Key, p => p.Value),
            floatingObjs.Count);

        void AnalyzePhysicsCluster(PhysicsClusterInfoProxy.Snapshot snapshot, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (clusters.TryGetValue(snapshot.Cluster.ID, out var anInf))
                anInf.Add(snapshot);
            else
                clusters.Add(snapshot.Cluster.ID, anInf = new(snapshot));

            // TODO: Filter parent events to prevent time overlap
            //switch (_event.Name)
            //{
            //default:
            //    break;
            //}

            anInf.TotalTicks += _event.ElapsedTicks;
            anInf.IncludedInGroups.Add(groupId);
            anInf.FramesCounted.Add(frameIndex);
        }

        void AnalyzeGrid(CubeGridInfoProxy.MotionSnapshot snapshot, ref readonly ProfilerEvent _event, int groupId, int frameIndex)
        {
            if (grids.TryGetValue(snapshot.BaseSnapshot.Grid.EntityId, out var anInf))
                anInf.Add(snapshot);
            else
                grids.Add(snapshot.BaseSnapshot.Grid.EntityId, anInf = new(snapshot));

            // TODO: Filter parent events to prevent time overlap
            //switch (_event.Name)
            //{
            //default:
            //    break;
            //}

            anInf.TotalTicks += _event.ElapsedTicks;
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

            var type = snapshot.Block.BlockType.Type;

            if (type == typeof(Sandbox.Game.Entities.Blocks.MyProgrammableBlock))
                progBlocks[eid] = info;

            // TODO: Filter parent events to prevent time overlap
            //switch (_event.Name)
            //{
            //default:
            //    break;
            //}

            info.TotalTicks += _event.ElapsedTicks;
            info.IncludedInProfilerGroups.Add(groupId);
            info.FramesCounted.Add(frameIndex);

            long timeByType;

            if (!blockTimesByType.TryGetValue(type, out timeByType))
                timeByType = 0;

            blockTimesByType[type] = timeByType + _event.ElapsedTicks;
        }
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
    public CubeBlockAnalysisInfo[] AllBlocks;
    public CubeBlockAnalysisInfo[] ProgrammableBlocks;
    public Dictionary<Type, double> BlockTimesByType;

    public int FloatingObjects;

    internal RecordingAnalysisInfo(FrameTimeInfo frameTimes, PhysicsClusterAnalysisInfo[] physicsClusters,
        CubeGridAnalysisInfo[] grids, CubeBlockAnalysisInfo[] allBlocks, CubeBlockAnalysisInfo[] programmableBlocks,
        Dictionary<Type, double> blockTimesByType, int floatingObjs)
    {
        FrameTimes = frameTimes;
        PhysicsClusters = physicsClusters;
        Grids = grids;
        AllBlocks = allBlocks;
        ProgrammableBlocks = programmableBlocks;
        BlockTimesByType = blockTimesByType;
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

        public double TotalTime => ProfilerTimer.MillisecondsFromTicks(TotalTicks);
        public long TotalTicks;

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
        HashSet<CubeGridInfoProxy.MotionSnapshot> snapshots = [];

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
        public bool? IsPowered; // Null value means mixed
        public HashSet<int> GroupIds = [];
        public HashSet<int> GroupSizes = [];
        public HashSet<int> ConnectedGrids = [];

        public HashSet<Vector3D> Positions = [];
        public HashSet<float> Speeds = [];
        public HashSet<int> PhysicsClusters = [];

        public HashSet<int> IncludedInGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime => ProfilerTimer.MillisecondsFromTicks(TotalTicks);
        public long TotalTicks;

        public double AverageTimePerFrame;

        public Builder(CubeGridInfoProxy.MotionSnapshot snapshot)
        {
            var b = snapshot.BaseSnapshot;
            var g = b.Grid;

            EntityId = g.EntityId;
            GridSize = g.GridSize;
            IsNPC = g.IsNPC;
            IsPreview = g.IsPreview;
            IsStatic = b.IsStatic;
            IsPowered = b.IsPowered;

            Add(snapshot);
        }

        public void Add(CubeGridInfoProxy.MotionSnapshot snapshot)
        {
            snapshots.Add(snapshot);

            var b = snapshot.BaseSnapshot;

            if (IsStatic != null && b.IsStatic != IsStatic)
                IsStatic = null;

            Names.Add(b.Name);
            Owners[b.OwnerId] = b.OwnerName;
            BlockCounts.Add(b.BlockCount);
            PCUs.Add(b.PCU);
            Sizes.Add(b.Size);

            if (IsPowered != null && b.IsPowered != IsPowered)
                IsPowered = null;

            GroupIds.Add(b.GroupId);
            GroupSizes.Add(b.GroupSize);
            ConnectedGrids.Add(b.ConnectedGrids);

            Positions.Add(snapshot.Position);
            Speeds.Add(snapshot.Speed);
            PhysicsClusters.Add(snapshot.PhysicsCluster);
        }

        public CubeGridAnalysisInfo Finish()
        {
            return new CubeGridAnalysisInfo(snapshots.Count, EntityId, GridSize, IsNPC, IsPreview, IsStatic,
                Names.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(),
                BlockCounts.ToArray(), PCUs.ToArray(), Sizes.ToArray(), IsPowered, GroupIds.ToArray(), GroupSizes.ToArray(),
                ConnectedGrids.ToArray(), Positions.ToArray(), Speeds.ToArray(), PhysicsClusters.ToArray(),
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

    public string IsPoweredForColumn => IsPowered == null ? "*" : IsPowered.Value.ToString();
    public string GroupIdForColumn => GroupIds.Length == 1 ? GroupIds[0].ToString() : string.Join(",\n", GroupIds);
    public string GroupSizeForColumn => GroupSizes.Length == 1 ? GroupSizes[0].ToString() : string.Join(",\n", GroupSizes);
    public string ConnectedGridsForColumn => ConnectedGrids.Length == 1 ? ConnectedGrids[0].ToString() : string.Join(",\n", ConnectedGrids);

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

    public CubeGridAnalysisInfo(
        int snapshotCount, long entityId, MyCubeSize gridSize, bool isNpc, bool isPreview, bool? isStatic,
        string[] names, (long ID, string? Name)[] owners, int[] blockCounts, int[] pcus, Vector3I[] sizes,
        bool? isPowered, int[] groupIds, int[] groupSizes, int[] connectedGrids,
        Vector3D[] positions, float[] speeds, int[] physicsClusters,
        double totalTime, double averageTimePerFrame, int includedInNumProfilerGroups, int numFramesCounted)
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
        IsPowered = isPowered;
        GroupIds = groupIds;
        GroupSizes = groupSizes;
        ConnectedGrids = connectedGrids;

        Positions = positions;
        Speeds = speeds;
        PhysicsClusters = physicsClusters;

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
        public Vector3D LastWorldPosition;
        public HashSet<int> IncludedInProfilerGroups = [];
        public HashSet<int> FramesCounted = [];

        public double TotalTime => ProfilerTimer.MillisecondsFromTicks(TotalTicks);
        public long TotalTicks;

        public double AverageTimePerFrame;

        public Builder(CubeBlockInfoProxy.Snapshot snapshot)
        {
            EntityId = snapshot.Block.EntityId;
            CubeSize = snapshot.GridSnapshot.Grid.GridSize;
            BlockType = snapshot.Block.BlockType.Type;

            Add(snapshot);
        }

        public void Add(CubeBlockInfoProxy.Snapshot snapshot)
        {
            snapshots.Add(snapshot);

            if (snapshot.CustomName != null)
                CustomNames.Add(snapshot.CustomName);

            GridIds.Add(snapshot.GridSnapshot.Grid.EntityId);
            Owners[snapshot.OwnerId] = snapshot.OwnerName;
            LastWorldPosition = snapshot.LastWorldPosition;
        }

        public CubeBlockAnalysisInfo Finish()
        {
            return new CubeBlockAnalysisInfo(snapshots.Count, EntityId, CubeSize, GridIds.ToArray(), BlockType,
                CustomNames.ToArray(), Owners.Select(o => (o.Key, o.Value)).ToArray(), LastWorldPosition,
                TotalTime, AverageTimePerFrame, IncludedInProfilerGroups.Count, FramesCounted.Count);
        }
    }

    public int SnapshotCount { get; }
    public long EntityId { get; }
    public MyCubeSize CubeSize { get; }
    public long[] GridIds;
    public Type BlockType;
    public string[] CustomNames;
    public (long ID, string? Name)[] Owners;
    public Vector3D LastWorldPosition;

    public double TotalTime { get; }
    public double AverageTimePerFrame { get; }
    public int IncludedInNumProfilerGroups { get; }
    public int NumFramesCounted { get; }

    public string GridIdsForColumn => GridIds.Length == 1 ? GridIds[0].ToString() : string.Join("\n", GridIds);
    public string CustomNamesForColumn => CustomNames.Length == 1 ? CustomNames[0] : string.Join("\n", CustomNames);
    public string OwnerIDsForColumn => string.Join("\n", Owners.Select(o => o.ID));
    public string OwnerNamesForColumn => string.Join("\n", Owners.Select(o => o.Name));

    public CubeBlockAnalysisInfo(
        int snapshotCount, long entityId, MyCubeSize cubeSize, long[] gridIds, Type blockType,
        string[] customNames, (long ID, string? Name)[] owners, Vector3D lastWorldPosition,
        double totalTime, double averageTimePerFrame, int includedInNumProfilerGroups, int numFramesCounted)
    {
        SnapshotCount = snapshotCount;
        EntityId = entityId;
        CubeSize = cubeSize;
        GridIds = gridIds;
        BlockType = blockType;
        CustomNames = customNames;
        Owners = owners;
        LastWorldPosition = lastWorldPosition;
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

        sb.AppendLine($"    Last Position{Vector3D.Round(LastWorldPosition, CubeSize == MyCubeSize.Large ? 0 : 1)}");

        sb.AppendLine($"Total Time: {TotalTime:N1}ms");
        sb.AppendLine($"Average Time: {AverageTimePerFrame:N2}ms");
        sb.AppendLine($"Counted Frames: {NumFramesCounted}");

        if (IncludedInNumProfilerGroups > 1)
            sb.AppendLine($"Processed over {IncludedInNumProfilerGroups} threads");

        return sb.ToString();
    }
}
