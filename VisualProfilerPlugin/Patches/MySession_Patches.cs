using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.Components;

namespace VisualProfiler.Patches;

[PatchShim]
static class MySession_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MySession.Load), _public: true, _static: true);

        var prepareBaseSession = typeof(MySession).GetNonPublicInstanceMethod("PrepareBaseSession", [typeof(MyObjectBuilder_Checkpoint), typeof(MyObjectBuilder_Sector)]);

        PatchPrefixSuffixPair(ctx, prepareBaseSession);
        PatchPrefixSuffixPair(ctx, "LoadWorld", _public: false, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MySession.GetWorld), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MySession.GetCheckpoint), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MySession.GetSector), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MySession.SaveDataComponents), _public: true, _static: false);

        var source = typeof(MySession).GetPublicInstanceMethod(nameof(MySession.UpdateComponents));
        var prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateComponents));

        ctx.GetPattern(source).Prefixes.Add(prefix);

        PatchPrefixSuffixPair(ctx, nameof(MySession.SendVicinityInformation), _public: true, _static: true);
    }

    static void PatchPrefixSuffixPair(PatchContext ctx, string methodName, bool _public, bool _static)
    {
        var source = typeof(MySession).GetMethod(methodName, _public, _static);

        PatchPrefixSuffixPair(ctx, source);
    }

    static void PatchPrefixSuffixPair(PatchContext ctx, MethodInfo source)
    {
        var prefix = typeof(MySession_Patches).GetNonPublicStaticMethod("Prefix_" + source.Name);
        var suffix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Load;
        internal static ProfilerKey PrepareBaseSession;
        internal static ProfilerKey LoadWorld;
        internal static ProfilerKey GetWorld;
        internal static ProfilerKey GetCheckpoint;
        internal static ProfilerKey GetSector;
        internal static ProfilerKey SaveDataComponents;
        internal static ProfilerKey UpdateComponents;
        internal static ProfilerKey SendVicinityInformation;

        internal static void Init()
        {
            Load = ProfilerKeyCache.GetOrAdd("MySession.Load");
            PrepareBaseSession = ProfilerKeyCache.GetOrAdd("MySession.PrepareBaseSession");
            LoadWorld = ProfilerKeyCache.GetOrAdd("MySession.LoadWorld");
            GetWorld = ProfilerKeyCache.GetOrAdd("MySession.GetWorld");
            GetCheckpoint = ProfilerKeyCache.GetOrAdd("MySession.GetCheckpoint");
            GetSector = ProfilerKeyCache.GetOrAdd("MySession.GetSector");
            SaveDataComponents = ProfilerKeyCache.GetOrAdd("MySession.SaveDataComponents");
            UpdateComponents = ProfilerKeyCache.GetOrAdd("MySession.UpdateComponents");
            SendVicinityInformation = ProfilerKeyCache.GetOrAdd("MySession.SendVicinityInformation");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    [MethodImpl(Inline)]
    static bool Prefix_Load(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start(Keys.Load, profileMemory: true, new(ProfilerEvent.EventCategory.Load));
        return true;
    }

    [MethodImpl(Inline)] static bool Prefix_PrepareBaseSession(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.PrepareBaseSession); return true; }

    [MethodImpl(Inline)] static bool Prefix_LoadWorld(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.LoadWorld); return true; }

    [MethodImpl(Inline)] static bool Prefix_GetWorld(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.GetWorld); return true; }

    [MethodImpl(Inline)] static bool Prefix_GetCheckpoint(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.GetCheckpoint); return true; }

    [MethodImpl(Inline)] static bool Prefix_GetSector(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.GetSector); return true; }

    [MethodImpl(Inline)] static bool Prefix_SaveDataComponents(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SaveDataComponents); return true; }

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
        Profiler.Start(Keys.UpdateComponents);

        var sessionComponentsForUpdate = __field_m_sessionComponentsForUpdate;
        bool gameReady = MySandboxGame.IsGameReady;

        Profiler.Start(0, "Before Simulation");
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

        if (MyMultiplayer.Static != null)
        {
            Profiler.Restart(2, "Simulate Replication Layer");
            MyMultiplayer.Static.ReplicationLayer.Simulate();
        }

        Profiler.Restart(3, "Simulate");
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

        Profiler.Restart(4, "After Simulation");
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
        Profiler.Stop();

        return false;
    }

    [MethodImpl(Inline)] static bool Prefix_SendVicinityInformation(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SendVicinityInformation); return true; }
}
