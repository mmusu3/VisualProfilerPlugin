using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AdvancedProfiler;

static class ReflectionHelper
{
    static T ThrowIfNull<T>(T? obj, string methodName, [CallerMemberName] string callerName = null!)
    {
        if (obj == null) throw new NullReferenceException($"{callerName} returned null looking for {methodName}.");

        return obj;
    }

    public static MethodInfo GetMethod(this Type type, string methodName, bool _public, bool _static)
    {
        return ThrowIfNull(type.GetMethod(methodName, (_public ? BindingFlags.Public : BindingFlags.NonPublic) | (_static ? BindingFlags.Static : BindingFlags.Instance)), methodName);
    }

    public static MethodInfo GetPublicStaticMethod(this Type type, string methodName)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static), methodName);
    }

    public static MethodInfo GetPublicInstanceMethod(this Type type, string methodName)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance), methodName);
    }

    public static MethodInfo GetNonPublicStaticMethod(this Type type, string methodName)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static), methodName);
    }

    public static MethodInfo GetNonPublicInstanceMethod(this Type type, string methodName)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance), methodName);
    }

    public static MethodInfo GetAnyStaticMethod(this Type type, string methodName)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), methodName);
    }

    public static MethodInfo GetAnyInstanceMethod(this Type type, string methodName)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), methodName);
    }

    public static MethodInfo GetPublicStaticMethod(this Type type, string methodName, Type[] paramTypes)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, paramTypes, null), methodName);
    }

    public static MethodInfo GetPublicInstanceMethod(this Type type, string methodName, Type[] paramTypes)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, paramTypes, null), methodName);
    }

    public static MethodInfo GetNonPublicStaticMethod(this Type type, string methodName, Type[] paramTypes)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static, null, paramTypes, null), methodName);
    }

    public static MethodInfo GetNonPublicInstanceMethod(this Type type, string methodName, Type[] paramTypes)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance, null, paramTypes, null), methodName);
    }

    public static MethodInfo GetAnyStaticMethod(this Type type, string methodName, Type[] paramTypes)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, paramTypes, null), methodName);
    }

    public static MethodInfo GetAnyInstanceMethod(this Type type, string methodName, Type[] paramTypes)
    {
        return ThrowIfNull(type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, paramTypes, null), methodName);
    }
}
