using System.Collections.Generic;
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
        var source = typeof(MySession).GetPublicInstanceMethod(nameof(MySession.UpdateComponents));
        var prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateComponents));

        ctx.GetPattern(source).Prefixes.Add(prefix);
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

    static bool Prefix_UpdateComponents(Dictionary<int, SortedSet<MySessionComponentBase>> __field_m_sessionComponentsForUpdate)
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

        Profiler.Restart("Simulate replication layer");
        MyMultiplayer.Static?.ReplicationLayer.Simulate();

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
}
