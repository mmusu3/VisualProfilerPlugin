using System;
using System.Reflection;
using System.Threading;
using ParallelTasks;
using Torch.Managers.PatchManager;

namespace AdvancedProfiler.Patches;

static class Worker_WorkerLoop_Patch
{
    public static void Patch()
    {
        Plugin.Log.Info("Initiating early patch of PrioritizedScheduler.Worker.WorkerLoop");

        var source = typeof(PrioritizedScheduler).GetNestedType("Worker", BindingFlags.NonPublic)!.GetNonPublicInstanceMethod("WorkerLoop");
        var target = typeof(Worker_WorkerLoop_Patch).GetNonPublicStaticMethod(nameof(Prefix));

        var decoratedMethodType = Type.GetType("Torch.Managers.PatchManager.DecoratedMethod, Torch")!;

        var decoratedMethod = (MethodRewritePattern)Activator.CreateInstance(decoratedMethodType,
            BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic, null, [ (MethodBase)source ], null)!;

        decoratedMethod.Prefixes.Add(target);
        decoratedMethodType.InvokeMember("Commit", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, decoratedMethod, null);

        Plugin.Log.Info("Early patch completed.");
    }

    static bool Prefix(Thread __field_m_thread, int __field_m_workerIndex)
    {
        var threadPriority = __field_m_thread.Priority;
        var groupName = "Parallel_" + threadPriority;

        Profiler.SetSortingGroupForCurrentThread(groupName, __field_m_workerIndex);
        Profiler.SetSortingGroupOrderPriority(groupName, 20 + (int)threadPriority);

        return true;
    }
}
