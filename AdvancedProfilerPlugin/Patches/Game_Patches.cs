using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox.Engine.Platform;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRageRender;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class Game_Patches
{
    public static void Patch(PatchContext ctx)
    {
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
        Profiler.Start("Wait for next update");
    }

    static IEnumerable<MsilInstruction> Transpile_UpdateInternal(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        Plugin.Log.Debug($"Patching {nameof(Game)}.UpdateInternal.");

        const int expectedParts = 4;
        int patchedParts = 0;

        var startMethod1 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [ typeof(string) ]);
        var startMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), paramTypes: [ typeof(int), typeof(string) ]);
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose));
        var endMethod = typeof(Game_Patches).GetNonPublicStaticMethod(nameof(EndFrame));

        var rpBeforeUpdateMethod = typeof(MyRenderProxy).GetPublicStaticMethod(nameof(MyRenderProxy.BeforeUpdate));
        var afterDrawMethod = typeof(Game).GetNonPublicInstanceMethod("AfterDraw");
        var updateMethod = typeof(Game).GetNonPublicInstanceMethod("Update");

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));

        yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("UpdateFrame");
        yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod1);
        yield return timerLocal1.AsValueStore();

        var instructions = instructionStream.ToArray();

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.Operand is MsilOperandInline<MethodBase> callOperand)
            {
                var callMethod = callOperand.Value;

                if (callMethod == rpBeforeUpdateMethod)
                {
                    yield return new MsilInstruction(OpCodes.Ldc_I4_0).SwapTryCatchOperations(ins);
                    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("MyRenderProxy.BeforeUpdate");
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod2);
                    yield return ins;
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
                    patchedParts++;
                    continue;
                }
                else if (callMethod == updateMethod)
                {
                    yield return ins;
                    yield return timerLocal2.AsValueLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
                    patchedParts++;
                    continue;
                }
            }
            else if (ins.OpCode == OpCodes.Ldarg_0)
            {
                if (instructions[i - 1].OpCode == OpCodes.Endfinally)
                {
                    yield return new MsilInstruction(OpCodes.Ldc_I4_1).SwapTryCatchOperations(ins);
                    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("UpdateInternal::Update");
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod2);
                    yield return timerLocal2.AsValueStore();
                    patchedParts++;
                }
                else if (instructions[i + 1].Operand is MsilOperandInline<MethodBase> callOperand2 && callOperand2.Value == afterDrawMethod)
                {
                    yield return new MsilInstruction(OpCodes.Ldc_I4_2).SwapTryCatchOperations(ins);
                    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("MyRenderProxy.AfterUpdate"); // MyRenderProxy.AfterUpdate() is called by MySandboxGame.AfterDraw()
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(startMethod2);
                    yield return ins;
                    yield return instructions[++i];
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(stopMethod);
                    patchedParts++;
                    continue;
                }
            }
            else if (ins.OpCode == OpCodes.Ret)
            {
                break;
            }

            yield return ins;
        }

        yield return timerLocal1.AsValueLoad();
        yield return new MsilInstruction(OpCodes.Call).InlineValue(disposeMethod);
        yield return new MsilInstruction(OpCodes.Call).InlineValue(endMethod);
        yield return new MsilInstruction(OpCodes.Ret);

        if (patchedParts != expectedParts)
            Plugin.Log.Fatal($"Failed to patch {nameof(Game)}.UpdateInternal. {patchedParts} out of {expectedParts} code parts matched.");
        else
            Plugin.Log.Debug("Patch successful.");
    }

    static readonly List<ProfilerGroup> profilerGroupsList = [];

    static void EndFrame()
    {
        Profiler.EndFrameForCurrentThread(ProfilerHelper.ProfilerEventObjectResolver);
        Profiler.GetProfilerGroups(profilerGroupsList);

        foreach (var item in profilerGroupsList)
        {
            if (item.SortingGroup == "Havok")
                item.EndFrame(ProfilerHelper.ProfilerEventObjectResolver);
        }

        profilerGroupsList.Clear();
    }
}
