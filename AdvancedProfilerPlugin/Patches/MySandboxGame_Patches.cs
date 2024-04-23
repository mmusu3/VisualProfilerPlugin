using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Havok;
using Sandbox;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Profiler;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MySandboxGame_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MySandboxGame).GetPublicInstanceMethod("Run");
        var prefix = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Prefix_Run));

        ctx.GetPattern(source).Prefixes.Add(prefix);

        source = typeof(MySandboxGame).GetNonPublicInstanceMethod("LoadData");
        var transpiler = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Transpile_LoadData));

        ctx.GetPattern(source).Transpilers.Add(transpiler);

        source = typeof(MySandboxGame).GetPublicInstanceMethod(nameof(MySandboxGame.ProcessInvoke));
        prefix = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Prefix_ProcessInvoke));
        var suffix = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(Suffix_ProcessInvoke));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Run()
    {
        Profiler.SetSortingGroupForCurrentThread("Main");
        Profiler.SetSortingGroupOrderPriority("Main", 100);
        return true;
    }

    static IEnumerable<MsilInstruction> Transpile_LoadData(IEnumerable<MsilInstruction> instructions)
    {
        Plugin.Log.Debug($"Patching {nameof(MySandboxGame)}.LoadData.");

        bool patched = false;

        var baseSystemInitMethod = typeof(HkBaseSystem).GetPublicStaticMethod(nameof(HkBaseSystem.Init),
            [ typeof(int), typeof(Action<string>), typeof(bool), typeof(VRage.Library.Threading.ISharedCriticalSection) ]);

        var initProfilingMethod = typeof(MySandboxGame_Patches).GetNonPublicStaticMethod(nameof(InitHavokProfiling));

        foreach (var ins in instructions)
        {
            yield return ins;

            if (ins.OpCode == OpCodes.Call && ins.Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == baseSystemInitMethod)
            {
                yield return new MsilInstruction(OpCodes.Call).InlineValue(initProfilingMethod);
                patched = true;
            }
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MySandboxGame)}.LoadData.");
    }

    static void InitHavokProfiling()
    {
        var taskNames = new string[HkTaskType.HK_JOB_TYPE_OTHER + 1 - HkTaskType.Schedule];

        for (int i = (int)HkTaskType.Schedule; i <= (int)HkTaskType.HK_JOB_TYPE_OTHER; i++)
            taskNames[i - (int)HkTaskType.Schedule] = ((MyProfiler.TaskType)i).ToString();

        // TODO: Replace impl of HkTaskProfiler.TaskStarted() to avoid ConcDict lookup since char* name is always empty.
        HkTaskProfiler.Init(OnTaskStarted, Profiler.Stop);

        void OnTaskStarted(string name, HkTaskType type)
        {
            int index = type - HkTaskType.Schedule;
            var taskName = index < taskNames.Length ? taskNames[index] : "UnknownTask";

            Profiler.Start(type - HkTaskType.Schedule, taskName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ProcessInvoke()
    {
        Profiler.Start("MySandboxGame.ProcessInvoke");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ProcessInvoke()
    {
        Profiler.Stop();
    }
}
