using System;
using System.Collections.Generic;
using Sandbox.Engine.Physics;
using Torch.Utils;
using VRageMath.Spatial;

namespace VisualProfiler;

static class PhysicsHelper
{
    [ReflectedGetter(Name = "ClusterObjectID", Type = typeof(MyPhysicsBody))]
    static Func<MyPhysicsBody, ulong> clusterObjectIDGetter = null!;

    [ReflectedGetter(Name = "m_objectsData", Type = typeof(MyClusterTree))]
    static Func<MyClusterTree, object> cluterTreeObjectsDataGetter = null!;

    delegate bool TryGetValueDelegate<TKey, TValue>(object dict, TKey key, out TValue value);

    static TryGetValueDelegate<ulong, object> tryGetValueMethod = null!;

    [ReflectedGetter(Name = "Cluster", TypeName = "VRageMath.Spatial.MyClusterTree+MyObjectData, VRage.Math")]
    static Func<object, MyClusterTree.MyCluster> objectDataClusterGetter = null!;

    static PhysicsHelper()
    {
        var objDataType = Type.GetType("VRageMath.Spatial.MyClusterTree+MyObjectData, VRage.Math")!;
        var dictType = typeof(Dictionary<,>).MakeGenericType([typeof(ulong), objDataType]);
        Type[] paramTypes = [typeof(ulong), objDataType.MakeByRefType()];

        ReflectionHelper.CreateMethodInvoker(out tryGetValueMethod, dictType, "TryGetValue", isStatic: false, paramTypes);
    }

    public static int GetClusterIdForObject(MyPhysicsBody? physicsObj)
    {
        if (physicsObj == null)
            return -1;

        ulong objId = clusterObjectIDGetter(physicsObj);

        var objDataDict = cluterTreeObjectsDataGetter(MyPhysics.Clusters);

        if (tryGetValueMethod(objDataDict, objId, out var objData))
        {
            var cluster = objectDataClusterGetter(objData);

            return cluster.ClusterId;
        }

        return -1;
    }
}
