using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Torch.Managers.PatchManager;

namespace VisualProfiler;

static class PatchHelper
{
    static readonly Type decoratedMethodType = Type.GetType("Torch.Managers.PatchManager.DecoratedMethod, Torch")!;

    public static MethodRewritePattern CreateRewritePattern(MethodBase method)
    {
        return (MethodRewritePattern)Activator.CreateInstance(decoratedMethodType,
            BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic, null, [method], null)!;
    }

    public static void CommitMethodPatches(MethodRewritePattern pattern)
    {
        decoratedMethodType.InvokeMember("Commit", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, pattern, null);
    }

    public static bool PatchPrefixSuffixPair(Type targetType, Type patchesType, PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        if (!targetType.TryGetMethod(methodName, _public, _static, out var source))
        {
            Plugin.Log.Error($"Failed to patch {targetType.Name}.{methodName}");
            return false;
        }

        var prefix = patchesType.GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(PatchHelper).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    public static bool TranspileMethod(Type targetType, Type patchesType, PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        if (!targetType.TryGetMethod(methodName, _public, _static, out var source))
        {
            Plugin.Log.Error($"Failed to patch {targetType.Name}.{methodName}");
            return false;
        }

        var transpiler = patchesType.GetNonPublicStaticMethod("Transpile_" + methodName);
        var pattern = patchContext.GetPattern(source);

        pattern.Transpilers.Add(transpiler);

        return true;
    }
}
