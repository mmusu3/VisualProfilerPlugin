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
        ProfilerHelper.InitThread(200 + 100 * (2 - (int)__field_m_thread.Priority / 2) + __field_m_workerIndex, __field_m_thread.Priority != ThreadPriority.Highest);

        return true;
    }
}
