using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;
using VRageMath.Spatial;

namespace VisualProfiler;

static class PhysicsHelper
{
    static Func<MyPhysicsBody, int> getClusterIdForObject = null!;

    static PhysicsHelper()
    {
        CreateClusterIdAccessor();
    }

    // Generates the function as such:
    //
    // public static int GetClusterIdForObject(MyPhysicsBody physicsObj)
    // {
    //     MyClusterTree.MyObjectData objData;
    //
    //     if (MyPhysics.Clusters.m_objectsData.TryGetValue(physicsObj.ClusterObjectID, out objData))
    //         return objData.Cluster.ClusterId;
    //
    //     return -1;
    // }
    static void CreateClusterIdAccessor()
    {
        // NOTE: AssemblyBuilder path requires extra work. See https://dotnetfiddle.net/YPkT7H

        //var assmBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("PhysicsHelper"), AssemblyBuilderAccess.Run);
        //var module = assmBuilder.DefineDynamicModule("<module>");
        //var methodBuilder = module.DefineGlobalMethod("GetClusterIdForObject", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(MyPhysicsBody)]);

        var method = new DynamicMethod("GetClusterIdForObject", typeof(int), [typeof(MyPhysicsBody)], restrictedSkipVisibility: true);

        method/*Builder*/.DefineParameter(1, ParameterAttributes.In, "physicsObj");

        var il = method/*Builder*/.GetILGenerator();

        il.Emit(OpCodes.Ldsfld, typeof(MyPhysics).GetField("Clusters")!);
        il.Emit(OpCodes.Ldfld, typeof(MyClusterTree).GetField("m_objectsData", BindingFlags.Instance | BindingFlags.NonPublic)!);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(MyPhysicsBody).GetProperty("ClusterObjectID", BindingFlags.Instance | BindingFlags.NonPublic)!.GetMethod!);

        var objDataType = typeof(MyClusterTree).GetNestedType("MyObjectData", BindingFlags.NonPublic)!;
        var local = il.DeclareLocal(objDataType);

        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Stloc, local);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Callvirt, typeof(Dictionary<,>).MakeGenericType([typeof(ulong), objDataType]).GetPublicInstanceMethod("TryGetValue"));

        var label = il.DefineLabel();

        il.Emit(OpCodes.Brfalse, label);
        il.Emit(OpCodes.Ldloc, local);
        il.Emit(OpCodes.Ldfld, objDataType.GetField("Cluster", BindingFlags.Instance | BindingFlags.Public)!);
        il.Emit(OpCodes.Ldfld, typeof(MyClusterTree.MyCluster).GetField("ClusterId", BindingFlags.Instance | BindingFlags.Public)!);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(label);
        il.Emit(OpCodes.Ldc_I4_M1);
        il.Emit(OpCodes.Ret);

        //module.CreateGlobalFunctions();

        //var method = module.GetMethod("GetClusterIdForObject")!;

        PatchUtilities.Compile(method);

        getClusterIdForObject = (Func<MyPhysicsBody, int>)method.CreateDelegate(typeof(Func<MyPhysicsBody, int>));
    }

    public static int GetClusterIdForObject(MyPhysicsBody? physicsObj)
    {
        if (physicsObj == null)
            return -1;

        return getClusterIdForObject(physicsObj);
    }
}
