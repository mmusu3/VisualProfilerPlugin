using System.Reflection;
using System.Threading;
using ParallelTasks;

namespace AdvancedProfiler.Patches;

static class Worker_WorkerLoop_Patch
{
    public static void Patch()
    {
        Plugin.Log.Info("Begining early patch of PrioritizedScheduler.Worker.WorkerLoop");

        var source = typeof(PrioritizedScheduler).GetNestedType("Worker", BindingFlags.NonPublic)!.GetNonPublicInstanceMethod("WorkerLoop");
        var target = typeof(Worker_WorkerLoop_Patch).GetNonPublicStaticMethod(nameof(Prefix));

        var pattern = PatchHelper.CreateRewritePattern(source);
        pattern.Prefixes.Add(target);

        PatchHelper.CommitMethodPatches(pattern);

        Plugin.Log.Info("Early patch completed.");
    }

    static bool Prefix(Thread __field_m_thread, int __field_m_workerIndex)
    {
        var threadPriority = __field_m_thread.Priority;
        var groupName = "Parallel_" + threadPriority;

        Profiler.SetSortingGroupForCurrentThread(groupName, __field_m_workerIndex);
        Profiler.SetSortingGroupOrderPriority(groupName, 20 + (int)threadPriority);

        if (threadPriority == ThreadPriority.Highest)
            Profiler.SetIsRealtimeThread(true);

        return true;
    }
}
