using System;
using System.Collections.Generic;
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
                   Center: {Vector3D.Round(AABB.Center, 0)}
                   Size: {Vector3D.Round(AABB.Size, 0)}
                   RigidBodies: {RigidBodyCount} (Active: {ActiveRigidBodyCount})
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
                {GridSize} Grid
                   EntityId: {EntityId}
                   CustomName: {CustomName}
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
                Block
                   EntityId: {EntityId}
                   CustomName: {CustomName}
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
                Character
                   EntityId: {EntityId}
                   IdentityId: {IdentityId}
                   PlatformId: {PlatformId}
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
                Voxel
                   EntityId: {EntityId}
                   Name: {Name}
                   Center: {Vector3D.Round(AABB.Center, 0)}
                   Size: {Vector3D.Round(AABB.Size, 0)}
                """;
    }
}
