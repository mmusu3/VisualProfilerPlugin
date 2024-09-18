using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyCubeGrid_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateBeforeSimulation100), _public: true, _static: false);

        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation10), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyCubeGrid.UpdateAfterSimulation100), _public: true, _static: false);

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
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation, profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation10, profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation100, profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation, profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation10, profileMemory: true, new(__instance)); return true; }

    [MethodImpl(Inline)] static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer, MyCubeGrid __instance)
    { __local_timer = Profiler.Start(Keys.UpdateAfterSimulation100, profileMemory: true, new(__instance)); return true; }

    static IEnumerable<MsilInstruction> Transpile_Dispatch(IEnumerable<MsilInstruction> instructionStream, MethodBody __methodBody, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyCubeGrid)}.Dispatch.");

        const int expectedParts = 1;
        int patchedParts = 0;

        var profilerKeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null);
        var startMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(ProfilerKey), typeof(bool)]);
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose));

        var invokeMethod = typeof(MyCubeGrid).GetNonPublicInstanceMethod("Invoke");
        var updateStruct = typeof(MyCubeGrid).GetNestedType("Update", BindingFlags.NonPublic)!;
        var callbackField = updateStruct.GetField("Callback")!;
        var newInvokeMethod = typeof(MyCubeGrid_Patches).GetNonPublicStaticMethod("Invoke");

        var pattern1 = new OpCode[] { OpCodes.Ldarg_0, OpCodes.Ldloca_S, OpCodes.Ldarg_1, OpCodes.Call };

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue(Keys.Dispatch.GlobalIndex));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerKeyCtor));
        Emit(new MsilInstruction(OpCodes.Ldc_I4_1)); // profilerMemory: true
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod));
        Emit(timerLocal.AsValueStore());

        for (int i = 0; i < instructions.Length; i++)
        {
            if (TranspileHelper.MatchOpCodes(instructions, i, pattern1)
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
                    Emit(new MsilInstruction(OpCodes.Ldloc_S).InlineValue(new MsilLocal(updateLocal.LocalIndex)));
                    Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(callbackField));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(newInvokeMethod));
                    patchedParts++;
                    i += pattern1.Length - 1;
                    continue;
                }
            }
            else if (instructions[i].OpCode == OpCodes.Ret)
            {
                break;
            }

            Emit(instructions[i]);
        }

        Emit(timerLocal.AsValueLoad());
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(disposeMethod));
        Emit(new MsilInstruction(OpCodes.Ret));

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
        using (Profiler.Start(callback.Method.Name, profileMemory: true, new(callback)))
            callback();
    }
}
