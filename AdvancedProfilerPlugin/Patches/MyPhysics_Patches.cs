using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Havok;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyPhysics_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyPhysics).GetPublicInstanceMethod(nameof(MyPhysics.LoadData));
        var target = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(Transpile_LoadData));

        ctx.GetPattern(source).Transpilers.Add(target);

        source = typeof(MyPhysics).GetNonPublicInstanceMethod("UnloadData");
        target = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(Transpile_UnloadData));

        ctx.GetPattern(source).Transpilers.Add(target);

        PatchPrefixSuffixPair(ctx, "SimulateInternal", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "ExecuteParallelRayCasts", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "StepWorldsMeasured", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "StepSingleWorld", _public: false, _static: false);

        // TODO: Transpiler
        PatchPrefixSuffixPair(ctx, "StepWorldsParallel", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "EnableOptimizations", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "DisableOptimizations", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateActiveRigidBodies", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, "UpdateCharacters", _public: false, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        MethodInfo source;

        if (_public)
        {
            if (_static)
                source = typeof(MyPhysics).GetPublicStaticMethod(methodName);
            else
                source = typeof(MyPhysics).GetPublicInstanceMethod(methodName);
        }
        else
        {
            if (_static)
                source = typeof(MyPhysics).GetNonPublicStaticMethod(methodName);
            else
                source = typeof(MyPhysics).GetNonPublicInstanceMethod(methodName);
        }

        var prefix = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyPhysics_Patches).GetNonPublicStaticMethod("Suffix_" + methodName);

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static IEnumerable<MsilInstruction> Transpile_LoadData(IEnumerable<MsilInstruction> instructions)
    {
        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.{nameof(MyPhysics.LoadData)}.");

        var m_jobQueueField = typeof(MyPhysics).GetField("m_jobQueue", BindingFlags.NonPublic | BindingFlags.Static)!;
        var m_threadPoolField = typeof(MyPhysics).GetField("m_threadPool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var initProfilingMethod = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(InitProfiling));

        bool patched = false;

        foreach (var ins in instructions)
        {
            yield return ins;

            if (ins.OpCode == OpCodes.Stsfld && ins.Operand is MsilOperandInline<FieldInfo> fieldOp && fieldOp.Value == m_jobQueueField)
            {
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(m_jobQueueField);
                yield return new MsilInstruction(OpCodes.Ldarg_0);
                yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(m_threadPoolField);
                yield return new MsilInstruction(OpCodes.Call).InlineValue(initProfilingMethod);
                patched = true;
            }
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.{nameof(MyPhysics.LoadData)}");
    }

    static void InitProfiling(HkJobQueue m_jobQueue, HkJobThreadPool m_threadPool)
    {
        HkTaskProfiler.HookJobQueue(m_jobQueue);

        m_threadPool.RunOnEachWorker(delegate
        {
            int thisThreadIndex = m_threadPool.GetThisThreadIndex();
            ProfilerHelper.InitThread(500 + thisThreadIndex, simulation: false);
        });
    }

    static IEnumerable<MsilInstruction> Transpile_UnloadData(IEnumerable<MsilInstruction> instructionStream)
    {
        Plugin.Log.Debug($"Patching {nameof(MyPhysics)}.UnloadData.");

        bool patched = false;

        var m_threadPoolField = typeof(MyPhysics).GetField("m_threadPool", BindingFlags.NonPublic | BindingFlags.Static)!;
        var disposeMethod = typeof(HkHandle).GetPublicInstanceMethod(nameof(HkHandle.Dispose));
        var removeThreadsMethod = typeof(MyPhysics_Patches).GetNonPublicStaticMethod(nameof(RemoveThreads));

        var instructions = instructionStream.ToArray();

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == OpCodes.Ldsfld && ins.Operand is MsilOperandInline<FieldInfo> fieldOp && fieldOp.Value == m_threadPoolField)
            {
                if (instructions[i + 1].OpCode == OpCodes.Callvirt && instructions[i + 1].Operand is MsilOperandInline<MethodBase> callOp && callOp.Value == disposeMethod)
                {
                    yield return new MsilInstruction(OpCodes.Ldarg_0);
                    yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(m_threadPoolField);
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(removeThreadsMethod);
                    patched = true;
                }
            }

            yield return ins;
        }

        if (patched)
            Plugin.Log.Debug("Patch successful.");
        else
            Plugin.Log.Error($"Failed to patch {nameof(MyPhysics)}.UnloadData.");
    }

    static void RemoveThreads(HkJobThreadPool m_threadPool)
    {
        m_threadPool.RunOnEachWorker(Profiler.RemoveGroupForCurrentThread);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SimulateInternal(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.SimulateInternal");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SimulateInternal(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_ExecuteParallelRayCasts(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.ExecuteParallelRayCasts");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_ExecuteParallelRayCasts(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_StepWorldsMeasured(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.StepWorldsMeasured");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_StepWorldsMeasured(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_StepSingleWorld(ref ProfilerTimer __local_timer)
    {
        // TODO: Record world info
        __local_timer = Profiler.Start("MyPhysics.StepSingleWorld");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_StepSingleWorld(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_StepWorldsParallel(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.StepWorldsParallel");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_StepWorldsParallel(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_EnableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.EnableOptimizations");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_EnableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_DisableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.DisableOptimizations");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_DisableOptimizations(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateActiveRigidBodies(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.UpdateActiveRigidBodies");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateActiveRigidBodies(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateCharacters(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyPhysics.UpdateCharacters");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_UpdateCharacters(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
