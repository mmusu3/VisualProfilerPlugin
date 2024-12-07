using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Engine.Platform;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using static VisualProfiler.TranspileHelper;

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
        Profiler.Start(Keys.WaitForNextUpdate, ProfilerTimerOptions.None, new(ProfilerEvent.EventCategory.Wait));
    }

    static IEnumerable<MsilInstruction> Transpile_UpdateInternal(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(Game)}.UpdateInternal.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var beginFrameMethod = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(BeginFrame));
        var endMethod = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(EndFrame));

        var updateCounterField = typeof(Game).GetField("m_updateCounter", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var updateMethod = typeof(Game).GetNonPublicInstanceMethod("Update");

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        e.Emit(Call(beginFrameMethod));

        e.EmitProfilerStartLongExtra(Keys.UpdateFrame,
            ProfilerTimerOptions.ProfileMemory, "Sim Frame Index: {0}", [
                new(OpCodes.Ldarg_0),
                LoadField(updateCounterField),
                new(OpCodes.Conv_I8)
            ]
        );

        e.Emit(timerLocal1.AsValueStore());

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.Operand is MsilOperandInline<MethodBase> callOperand)
            {
                var callMethod = callOperand.Value;

                if (callMethod == updateMethod)
                {
                    e.Emit(ins);
                    e.EmitStopProfilerTimer(timerLocal2);
                    patchedParts++;
                    continue;
                }
            }
            else if (ins.OpCode == OpCodes.Ldarg_0)
            {
                if (instructions[i - 1].OpCode == OpCodes.Endfinally)
                {
                    e.EmitProfilerStart(1, "UpdateInternal::Update")[0].SwapTryCatchOperations(ref ins);
                    e.Emit(timerLocal2.AsValueStore());
                    patchedParts++;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            e.Emit(ins);
        }

        e.EmitDisposeProfilerTimer(timerLocal1);
        e.Emit(Call(endMethod));
        e.Emit(new(OpCodes.Ret));

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
