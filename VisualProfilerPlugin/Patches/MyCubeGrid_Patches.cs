using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using static VisualProfiler.TranspileHelper;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyCubeGrid_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        //PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation), _public: true, _static: false);
        //PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation10), _public: true, _static: false);
        //PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation100), _public: true, _static: false);

        //PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation), _public: true, _static: false);
        //PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation10), _public: true, _static: false);
        //PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation100), _public: true, _static: false);

        var source = typeof(MyCubeGrid).GetNonPublicInstanceMethod("Dispatch");
        var transpiler = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod(nameof(Transpile_Dispatch));
        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyCubeGrid).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey UpdateBeforeSimulation;
        internal static ProfilerKey UpdateBeforeSimulation10;
        internal static ProfilerKey UpdateBeforeSimulation100;
        internal static ProfilerKey UpdateAfterSimulation;
        internal static ProfilerKey UpdateAfterSimulation10;
        internal static ProfilerKey UpdateAfterSimulation100;
        internal static ProfilerKey Dispatch;

        internal static void Init()
        {
            UpdateBeforeSimulation = ProfilerKeyCache.GetOrAdd("MyCubeGrid.UpdateBeforeSimulation");
            UpdateBeforeSimulation10 = ProfilerKeyCache.GetOrAdd("MyCubeGrid.UpdateBeforeSimulation10");
            UpdateBeforeSimulation100 = ProfilerKeyCache.GetOrAdd("MyCubeGrid.UpdateBeforeSimulation100");
            UpdateAfterSimulation = ProfilerKeyCache.GetOrAdd("MyCubeGrid.UpdateAfterSimulation");
            UpdateAfterSimulation10 = ProfilerKeyCache.GetOrAdd("MyCubeGrid.UpdateAfterSimulation10");
            UpdateAfterSimulation100 = ProfilerKeyCache.GetOrAdd("MyCubeGrid.UpdateAfterSimulation100");
            Dispatch = ProfilerKeyCache.GetOrAdd("MyCubeGrid.Dispatch");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation, ProfilerTimerOptions.ProfileMemory, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation10, ProfilerTimerOptions.ProfileMemory, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation100, ProfilerTimerOptions.ProfileMemory, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation, ProfilerTimerOptions.ProfileMemory, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation10, ProfilerTimerOptions.ProfileMemory, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation100, ProfilerTimerOptions.ProfileMemory, new(__instance)); return true; }

    static IEnumerable<MsilInstruction> Transpile_Dispatch(IEnumerable<MsilInstruction> instructionStream, MethodBody __methodBody, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(MyCubeGrid)}.Dispatch.");

        const int expectedParts = 1;
        int patchedParts = 0;

        var invokeMethod = typeof(MyCubeGrid).GetNonPublicInstanceMethod("Invoke");
        var updateStruct = typeof(MyCubeGrid).GetNestedType("Update", BindingFlags.NonPublic)!;
        var callbackField = updateStruct.GetField("Callback")!;
        var newInvokeMethod = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod("Invoke");

        ReadOnlySpan<OpCode> pattern1 = [OpCodes.Ldarg_0, OpCodes.Ldloca_S, OpCodes.Ldarg_1, OpCodes.Call];

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        e.EmitProfilerStart(Keys.Dispatch, ProfilerTimerOptions.ProfileMemory);
        e.StoreLocal(timerLocal);

        for (int i = 0; i < instructions.Length; i++)
        {
            if (MatchOpCodes(instructions, i, pattern1)
                && instructions[i + 1].Operand is MsilOperandInline<MsilLocal> local && local.Value.Index == 4
                && instructions[i + 3].OpCode == OpCodes.Call && instructions[i + 3].Operand is MsilOperandInline<MethodBase> call && call.Value == invokeMethod)
            {
                var updateLocal = __methodBody.LocalVariables.ElementAtOrDefault(4);

                if (updateLocal == null || updateLocal.LocalType != updateStruct)
                {
                    Plugin.Log.Error($"Failed to patch {nameof(MyCubeGrid)}.Dispatch. Failed to find Update local variable.");
                }
                else
                {
                    e.LoadLocal(updateLocal);
                    e.LoadField(callbackField);
                    e.Call(newInvokeMethod);
                    patchedParts++;
                    i += pattern1.Length - 1;
                    continue;
                }
            }
            else if (instructions[i].OpCode == OpCodes.Ret)
            {
                break;
            }

            e.Emit(instructions[i]);
        }

        e.EmitDisposeProfilerTimer(timerLocal);
        e.Emit(new(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyCubeGrid)}.Dispatch. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Invoke(Action callback)
    {
        using (Profiler.Start(callback.Method.Name, ProfilerTimerOptions.ProfileMemory, new(callback)))
            callback();
    }
}
