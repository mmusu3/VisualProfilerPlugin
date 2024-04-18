using System.Threading;

namespace AdvancedProfiler.Patches;

static class ProfilerHelper
{
    internal static void InitThread(int viewPriority, bool simulation)
    {
        if (viewPriority < 200)
            return;

        var threadPriority = Thread.CurrentThread.Priority;
        int groupViewPrio = 200 + (2 - (int)threadPriority / 2) * 100;
        int workerIndex = viewPriority - groupViewPrio;

        var groupName = "Parallel_" + threadPriority;
        Profiler.SetSortingGroupForCurrentThread(groupName, -workerIndex);
        Profiler.SetSortingGroupOrderPriority(groupName, 20 + (int)threadPriority);
    }
}
