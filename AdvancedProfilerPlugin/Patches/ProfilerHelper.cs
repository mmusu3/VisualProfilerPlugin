using System;
using System.Collections.Generic;
using Havok;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game;
using VRageMath;
using VRageMath.Spatial;

namespace AdvancedProfiler.Patches;

static class ProfilerHelper
{
    internal static readonly ResolveProfilerEventObjectDelegate ProfilerEventObjectResolver = ResolveProfilerEventObject;

    static void ResolveProfilerEventObject(Dictionary<object, object> cache, ref ProfilerEvent _event)
    {
        switch (_event.ExtraObject)
        {
        case MyClusterTree.MyCluster cluster:
            {
                _event.ExtraValueFormat = "{0}";

                if (cache.TryGetValue(cluster, out var cachedObj))
                {
                    _event.ExtraObject = (PhysicsClusterInfoProxy)cachedObj;
                    break;
                }

                cache[cluster] = _event.ExtraObject = new PhysicsClusterInfoProxy(cluster);
            }
            break;
        case MyCubeGrid grid:
            {
                _event.ExtraValueFormat = "{0}";

                if (cache.TryGetValue(grid, out var cachedObj))
                {
                    _event.ExtraObject = (CubeGridInfoProxy)cachedObj;
                    break;
                }

                cache[grid] = _event.ExtraObject = new CubeGridInfoProxy(grid);
            }
            break;
        default:
            break;
        }
    }
}

class PhysicsClusterInfoProxy
{
    public BoundingBoxD AABB;
    public bool HasWorld;
    public int RigidBodyCount;
    public int ActiveRigidBodyCount;
    public int CharacterCount;

    public PhysicsClusterInfoProxy(MyClusterTree.MyCluster cluster)
    {
        var hkWorld = cluster.UserData as HkWorld;

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
                Physics Cluster
                   Center: {Vector3D.Round(AABB.Center)}
                   Size: {Vector3D.Round(AABB.Size)}
                   RigidBodies: {RigidBodyCount} (Active: {ActiveRigidBodyCount})
                   Characters: {CharacterCount}
                """;
    }
}

class CubeGridInfoProxy
{
    public long EntityId;
    public MyCubeSize GridSize;
    public long OwnerId;
    public string? OwnerName;
    public int BlockCount;

    public CubeGridInfoProxy(MyCubeGrid grid)
    {
        long ownerId = 0;

        if (grid.BigOwners.Count > 0)
            ownerId = grid.BigOwners[0];

        var ownerIdentity = MySession.Static.Players.TryGetIdentity(ownerId);
        //var ownerFaction = MySession.Static.Factions.GetPlayerFaction(ownerId);

        EntityId = grid.EntityId;
        GridSize = grid.GridSizeEnum;
        OwnerId = ownerId;
        OwnerName = ownerIdentity?.DisplayName;
        BlockCount = grid.BlocksCount;
    }

    public override string ToString()
    {
        var ownerNamePart = OwnerName != null ? $"{Environment.NewLine}   OwnerName: {OwnerName}" : null;

        return $"""
                {GridSize} Grid
                   EntityId: {EntityId}
                   OwnerId: {OwnerId}{ownerNamePart}
                   Blocks: {BlockCount}
                """;
    }
}
