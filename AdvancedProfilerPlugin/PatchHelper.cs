using System;
using System.Reflection;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler;

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
}
