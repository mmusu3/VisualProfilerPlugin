using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Engine.Platform;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler.Patches;

[PatchShim]
static class Game_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        var source = typeof(Game).GetPublicInstanceMethod("RunSingleFrame");
        var prefix = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(Prefix_RunSingleFrame));
        var suffix = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(Suffix_RunSingleFrame));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(Game).GetNonPublicInstanceMethod("UpdateInternal");
        var transpiler = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(Transpile_UpdateInternal));

        ctx.GetPattern(source).Transpilers.Add(transpiler);
    }

    static class Keys
    {
        internal static ProfilerKey WaitForNextUpdate;
        internal static ProfilerKey UpdateFrame;

        internal static void Init()
        {
            WaitForNextUpdate = ProfilerKeyCache.GetOrAdd("Wait for next update");
            UpdateFrame = ProfilerKeyCache.GetOrAdd("UpdateFrame");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_RunSingleFrame(Game __instance)
    {
        if (__instance.IsFirstUpdateDone)
            Profiler.Stop();

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_RunSingleFrame()
    {
        Profiler.Start(Keys.WaitForNextUpdate, profileMemory: false, new(ProfilerEvent.EventCategory.Wait));
    }

    static IEnumerable<MsilInstruction> Transpile_UpdateInternal(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));

        void Emit(MsilInstruction ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(Game)}.UpdateInternal.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var beginFrameMethod = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(BeginFrame));
        var profilerKeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null);
        var extraDataCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null);
        var startMethod1 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(ProfilerKey), typeof(bool), typeof(ProfilerEvent.ExtraData)]);
        var startMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [typeof(int), typeof(string)]);
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose));
        var endMethod = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(EndFrame));

        var updateCounterField = typeof(Game).GetField("m_updateCounter", BindingFlags.NonPublic | BindingFlags.Instance);
        var updateMethod = typeof(Game).GetNonPublicInstanceMethod("Update");

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        Emit(new MsilInstruction(OpCodes.Call).InlineValue(beginFrameMethod));
        Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue(Keys.UpdateFrame.GlobalIndex));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(profilerKeyCtor));
        Emit(new MsilInstruction(OpCodes.Ldc_I4_1)); // profilerMemory: true
        Emit(new MsilInstruction(OpCodes.Ldarg_0));
        Emit(new MsilInstruction(OpCodes.Ldfld).InlineValue(updateCounterField));
        Emit(new MsilInstruction(OpCodes.Conv_I8));
        Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("Sim Frame Index: {0}"));
        Emit(new MsilInstruction(OpCodes.Newobj).InlineValue(extraDataCtor));
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod1));
        Emit(timerLocal1.AsValueStore());

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.Operand is MsilOperandInline<MethodBase> callOperand)
            {
                var callMethod = callOperand.Value;

                if (callMethod == updateMethod)
                {
                    Emit(ins);
                    Emit(timerLocal2.AsValueLoad());
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(stopMethod));
                    patchedParts++;
                    continue;
                }
            }
            else if (ins.OpCode == OpCodes.Ldarg_0)
            {
                if (instructions[i - 1].OpCode == OpCodes.Endfinally)
                {
                    Emit(new MsilInstruction(OpCodes.Ldc_I4_1).SwapTryCatchOperations(ins));
                    Emit(new MsilInstruction(OpCodes.Ldstr).InlineValue("UpdateInternal::Update"));
                    Emit(new MsilInstruction(OpCodes.Call).InlineValue(startMethod2));
                    Emit(timerLocal2.AsValueStore());
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            Emit(ins);
        }

        Emit(timerLocal1.AsValueLoad());
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(disposeMethod));
        Emit(new MsilInstruction(OpCodes.Call).InlineValue(endMethod));
        Emit(new MsilInstruction(OpCodes.Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(Game)}.UpdateInternal. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    static readonly List<ProfilerGroup> profilerGroupsList = [];

    static void BeginFrame()
    {
        Profiler.BeginFrameForCurrentThread();
        Profiler.GetProfilerGroups(profilerGroupsList);

        foreach (var item in profilerGroupsList)
        {
            if (item.SortingGroup == "Parallel_Highest" || item.SortingGroup == "Havok")
                item.BeginFrame();
        }

        profilerGroupsList.Clear();
    }

    static void EndFrame()
    {
        Profiler.EndFrameForCurrentThread();
        Profiler.GetProfilerGroups(profilerGroupsList);

        foreach (var item in profilerGroupsList)
        {
            if (item.SortingGroup == "Parallel_Highest" || item.SortingGroup == "Havok")
                item.EndFrame();
        }

        profilerGroupsList.Clear();

        Profiler.EndOfFrame();
    }
}
