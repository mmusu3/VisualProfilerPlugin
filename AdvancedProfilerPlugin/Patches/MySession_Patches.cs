using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game.Components;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MySession_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MySession).GetPublicInstanceMethod(nameof(MySession.GetCheckpoint));
        var prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_GetCheckpoint));
        var suffix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Suffix_GetCheckpoint));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MySession).GetPublicInstanceMethod(nameof(MySession.GetSector));
        prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_GetSector));
        suffix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Suffix_GetSector));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MySession).GetPublicInstanceMethod(nameof(MySession.SaveDataComponents));
        prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_SaveDataComponents));
        suffix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Suffix_SaveDataComponents));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MySession).GetPublicInstanceMethod(nameof(MySession.UpdateComponents));
        prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateComponents));

        ctx.GetPattern(source).Prefixes.Add(prefix);

        source = typeof(MySession).GetPublicStaticMethod(nameof(MySession.SendVicinityInformation));
        prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_SendVicinityInformation));
        suffix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Suffix_SendVicinityInformation));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_GetCheckpoint(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MySession.GetCheckpoint");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_GetCheckpoint(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_GetSector(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MySession.GetSector");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_GetSector(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SaveDataComponents(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MySession.SaveDataComponents");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SaveDataComponents(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    //static IEnumerable<MsilInstruction> Transpile_UpdateComponents(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    //{
    //    Plugin.Log.Debug($"Patching {nameof(MySession)}.{nameof(MySession.UpdateComponents)}.");

    //    const int expectedParts = ;
    //    int patchedParts = 0;

    //    var profilerStartMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [ typeof(string) ]);
    //    var profilerStopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop));

    //    var timerLocal1 = __localCreator(typeof(ProfilerTimer));
    //    var timerLocal2 = __localCreator(typeof(ProfilerTimer));

    //    yield return new MsilInstruction(OpCodes.Ldstr).InlineValue("Before Simulation");
    //    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod);
    //    yield return timerLocal1.AsValueStore();

    //    var instructions = instructionStream.ToArray();

    //    for (int i = 0; i < instructions.Length; i++)
    //    {
    //        var ins = instructions[i];

    //        if (ins.Operand is MsilOperandInline<MethodBase> call1)
    //        {
    //            if (call1.Value == )
    //            {
    //                yield return new MsilInstruction(OpCodes.Ldstr).InlineValue();
    //                yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStartMethod);
    //                yield return timerLocal.AsValueStore();
    //                patchedParts++;
    //            }
    //        }
    //        else if (ins.OpCode == OpCodes.Ldloc_0 && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call2)
    //        {
    //            if (call2.Value == )
    //            {
    //                yield return timerLocal.AsValueLoad().SwapTryCatchOperations(ins);
    //                yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
    //                patchedParts++;
    //            }
    //        }
    //        else if (ins.OpCode == OpCodes.Ret)
    //        {
    //            break;
    //        }

    //        yield return ins;
    //    }

    //    yield return timerLocal1.AsValueLoad();
    //    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerStopMethod);
    //    yield return new MsilInstruction(OpCodes.Ret);

    //    if (patchedParts != expectedParts)
    //        Plugin.Log.Error($"Failed to patch {nameof(MySession)}.{nameof(MySession.UpdateComponents)}. {patchedParts} out of {expectedParts} code parts matched.");
    //    else
    //        Plugin.Log.Debug("Patch successful.");
    //}

    static bool Prefix_UpdateComponents(MySession __instance, Dictionary<int, SortedSet<MySessionComponentBase>> __field_m_sessionComponentsForUpdate)
    {
        var sessionComponentsForUpdate = __field_m_sessionComponentsForUpdate;
        bool gameReady = MySandboxGame.IsGameReady;

        Profiler.Start("Before simulation");
        {
            if (sessionComponentsForUpdate.TryGetValue((int)MyUpdateOrder.BeforeSimulation, out var beforeSimComps))
            {
                foreach (var component in beforeSimComps)
                {
                    if (gameReady || component.UpdatedBeforeInit())
                    {
                        using (Profiler.Start(component.DebugName))
                            component.UpdateBeforeSimulation();
                    }
                }
            }
        }

        if (__instance.ComponentAssetModifiers != null)
        {
            Profiler.Restart("ComponentAssetModifiers.RunRemoval");
            __instance.ComponentAssetModifiers.RunRemoval();
        }

        if (MyMultiplayer.Static != null)
        {
            Profiler.Restart("Simulate replication layer");
            MyMultiplayer.Static.ReplicationLayer.Simulate();
        }

        Profiler.Restart("Simulate");
        {
            if (sessionComponentsForUpdate.TryGetValue((int)MyUpdateOrder.Simulation, out var simComps))
            {
                foreach (var component in simComps)
                {
                    if (gameReady || component.UpdatedBeforeInit())
                    {
                        using (Profiler.Start(component.DebugName))
                            component.Simulate();
                    }
                }
            }
        }

        Profiler.Restart("After simulation");
        {
            if (sessionComponentsForUpdate.TryGetValue((int)MyUpdateOrder.AfterSimulation, out var afterSimComps))
            {
                foreach (var component in afterSimComps)
                {
                    if (gameReady || component.UpdatedBeforeInit())
                    {
                        using (Profiler.Start(component.DebugName))
                            component.UpdateAfterSimulation();
                    }
                }
            }
        }

        Profiler.Stop();

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_SendVicinityInformation(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MySession.SendVicinityInformation");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_SendVicinityInformation(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
