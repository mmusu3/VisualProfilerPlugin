using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace VisualProfiler;

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

    public static void CreateMethodInvoker<TDelegate>(out TDelegate invoker, Type declaringType, string methodName, bool isStatic, Type[]? parameterTypes)
        where TDelegate : Delegate
    {
        invoker = CreateMethodInvoker<TDelegate>(declaringType, methodName, isStatic, parameterTypes);
    }

    public static TDelegate CreateMethodInvoker<TDelegate>(Type declaringType, string methodName, bool isStatic, Type[]? parameterTypes)
        where TDelegate : Delegate
    {
        return (TDelegate)CreateMethodInvoker(typeof(TDelegate), declaringType, methodName, isStatic, parameterTypes);
    }

    public static Delegate CreateMethodInvoker(Type delegateType, Type declaringType, string methodName, bool isStatic, Type[]? parameterTypes)
    {
        var delegateInvokeMethod = delegateType.GetMethod("Invoke")!;
        var parameters = delegateInvokeMethod.GetParameters();
        var trueType = declaringType;

        Type[] trueParameterTypes;

        if (isStatic)
        {
            trueParameterTypes = parameters.Select(x => x.ParameterType).ToArray();
        }
        else
        {
            trueType ??= parameters[0].ParameterType;
            trueParameterTypes = parameters.Skip(1).Select(x => x.ParameterType).ToArray();
        }

        var invokeTypes = new Type[trueParameterTypes.Length];

        for (var i = 0; i < invokeTypes.Length; i++)
            invokeTypes[i] = parameterTypes?[i] ?? trueParameterTypes[i];

        var method = trueType.GetMethod(methodName,
            (isStatic ? BindingFlags.Static : BindingFlags.Instance) |
            BindingFlags.Public | BindingFlags.NonPublic,
            null, CallingConventions.Any, invokeTypes, null);

        if (method == null)
        {
            var methodType = isStatic ? "static" : "instance";
            var methodParamNames = string.Join(", ", trueParameterTypes.Select(x => x.Name));

            throw new ArgumentException($"Unable to find {methodType} method {methodName} in type {trueType.FullName} with parameters {methodParamNames}");
        }

        Delegate methodDelegate;

        if (!isStatic || parameterTypes != null)
        {
            var paramExpressions = parameters.Select(x => Expression.Parameter(x.ParameterType, x.Name)).ToArray();
            var argExpressions = new Expression[invokeTypes.Length];

            List<(ParameterExpression Param, ParameterExpression Local)>? outExprs = null;

            int pOffset = isStatic ? 0 : 1;

            for (var i = 0; i < argExpressions.Length; i++)
            {
                var param = parameters[pOffset + i];
                var paramExp = paramExpressions[pOffset + i];
                var invokeType = invokeTypes[i];
                Expression argExp = paramExp;

                if (paramExp.Type != invokeType)
                {
                    if (param.IsOut)
                    {
                        var local = Expression.Variable(invokeType.GetElementType()!);

                        argExp = local;
                        (outExprs ??= []).Add((paramExp, local));
                    }
                    else
                    {
                        argExp = Expression.Convert(paramExp, invokeType);
                    }
                }

                argExpressions[i] = argExp;
            }

            Expression call;

            if (!isStatic)
            {
                Debug.Assert(method.DeclaringType != null);

                Expression instanceExp = paramExpressions[0];

                if (instanceExp.Type != method.DeclaringType)
                    instanceExp = Expression.Convert(instanceExp, method.DeclaringType);

                call = Expression.Call(instanceExp, method, argExpressions);
            }
            else
            {
                call = Expression.Call(method, argExpressions);
            }

            Expression body;

            if (outExprs != null)
            {
                List<ParameterExpression> variables = [];
                List<Expression> statements = [];

                var retLocal = Expression.Variable(method.ReturnType);

                variables.Add(retLocal);
                statements.Add(Expression.Assign(retLocal, call));

                foreach (var item in outExprs)
                {
                    variables.Add(item.Local);
                    statements.Add(Expression.Assign(item.Param, Expression.Convert(item.Local, item.Param.Type)));
                }

                statements.Add(retLocal);

                body = Expression.Block(variables, statements);
            }
            else
            {
                body = call;
            }

            methodDelegate = Expression.Lambda(delegateType, body, paramExpressions).Compile();
        }
        else
        {
            methodDelegate = Delegate.CreateDelegate(delegateType, method);
        }

        return methodDelegate;
    }
}
