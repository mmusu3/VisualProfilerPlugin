﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Game;
using VRage.Game.Components;
using static System.Reflection.Emit.OpCodes;
using static VisualProfiler.TranspileHelper;

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
        //var prefix = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Prefix_UpdateComponents));
        var transpiler = typeof(MySession_Patches).GetNonPublicStaticMethod(nameof(Transpile_UpdateComponents));

        //ctx.GetPattern(source).Prefixes.Add(prefix);
        ctx.GetPattern(source).Transpilers.Add(transpiler);

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
        __local_timer = Profiler.Start(Keys.Load, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.Load));
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

    static bool Prefix_UpdateComponents(Dictionary<int, SortedSet<MySessionComponentBase>> __field_m_sessionComponentsForUpdate)
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

        MyMultiplayer.Static?.ReplicationLayer.Simulate();

        Profiler.Restart(1, "Simulate");
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

        Profiler.Restart(2, "After Simulation");
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

    static IEnumerable<MsilInstruction> Transpile_UpdateComponents(IEnumerable<MsilInstruction> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<MsilInstruction>((int)(instructions.Length * 1.1f));
        var e = newInstructions;

        Plugin.Log.Debug($"Patching {nameof(MySession)}.{nameof(MySession.UpdateComponents)}.");

        const int expectedParts = 6;
        int patchedParts = 0;

        var compsField = typeof(MySession).GetField("m_sessionComponentsForUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var tryGetValueMethod = typeof(Dictionary<int, SortedSet<MySessionComponentBase>>).GetPublicInstanceMethod("TryGetValue")!;
        var debugNameField = typeof(MySessionComponentBase).GetField(nameof(MySessionComponentBase.DebugName), BindingFlags.Instance | BindingFlags.Public)!;
        var updateBeforeSimMethod = typeof(MySessionComponentBase).GetPublicInstanceMethod(nameof(MySessionComponentBase.UpdateBeforeSimulation))!;
        var simulateMethod = typeof(MySessionComponentBase).GetPublicInstanceMethod(nameof(MySessionComponentBase.Simulate))!;
        var updateAfterSimMethod = typeof(MySessionComponentBase).GetPublicInstanceMethod(nameof(MySessionComponentBase.UpdateAfterSimulation))!;

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        ReadOnlySpan<OpCode> pattern1 = [Ldarg_0, Ldfld, Ldc_I4_1, Ldloca_S, Callvirt, Brfalse_S];
        ReadOnlySpan<OpCode> pattern11 = [Ldloc_2, Callvirt];
        ReadOnlySpan<OpCode> pattern2 = [Ldarg_0, Ldfld, Ldc_I4_2, Ldloca_S, Callvirt, Brfalse_S];
        ReadOnlySpan<OpCode> pattern22 = [Ldloc_3, Callvirt];
        ReadOnlySpan<OpCode> pattern3 = [Ldarg_0, Ldfld, Ldc_I4_4, Ldloca_S, Callvirt, Brfalse_S];
        ReadOnlySpan<OpCode> pattern33 = [Ldloc_S, Callvirt];

        e.EmitProfilerStart(Keys.UpdateComponents, ProfilerTimerOptions.ProfileMemory);
        e.Emit(new(Pop));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (MatchOpCodes(instructions, i, pattern1))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<FieldInfo> field && field.Value == compsField
                    && instructions[i + 4].Operand is MsilOperandInline<MethodBase> call && call.Value == tryGetValueMethod)
                {
                    e.EmitProfilerStart(0, "Before Simulation");
                    e.Emit(new(Pop));
                    patchedParts++;
                }
            }
            else if (MatchOpCodes(instructions, i, pattern11))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<MethodBase> call && call.Value == updateBeforeSimMethod)
                {
                    e.EmitProfilerStartName([
                        LoadLocal(2).SwapLabels(ref ins),
                        LoadField(debugNameField)
                    ]);
                    e.StoreLocal(timerLocal);
                    // Move past existing ops
                    e.Emit(ins);
                    e.Emit(instructions[++i]);
                    // Dispose timer
                    e.EmitDisposeProfilerTimer(timerLocal);
                    patchedParts++;
                    continue;
                }
            }
            else if (MatchOpCodes(instructions, i, pattern2))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<FieldInfo> field && field.Value == compsField
                    && instructions[i + 4].Operand is MsilOperandInline<MethodBase> call && call.Value == tryGetValueMethod)
                {
                    e.EmitProfilerRestart(1, "Simulate", ProfilerTimerOptions.ProfileMemory)[0].SwapLabelsAndTryCatchOperations(ref ins);
                    e.Emit(new(Pop));
                    patchedParts++;
                }
            }
            else if (MatchOpCodes(instructions, i, pattern22))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<MethodBase> call && call.Value == simulateMethod)
                {
                    e.EmitProfilerStartName([
                        LoadLocal(3).SwapLabels(ref ins),
                        LoadField(debugNameField)
                    ]);
                    e.StoreLocal(timerLocal);
                    // Move past existing ops
                    e.Emit(ins);
                    e.Emit(instructions[++i]);
                    // Dispose timer
                    e.EmitDisposeProfilerTimer(timerLocal);
                    patchedParts++;
                    continue;
                }
            }
            else if (MatchOpCodes(instructions, i, pattern3))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<FieldInfo> field && field.Value == compsField
                    && instructions[i + 4].Operand is MsilOperandInline<MethodBase> call && call.Value == tryGetValueMethod)
                {
                    e.EmitProfilerRestart(2, "After Simulation", ProfilerTimerOptions.ProfileMemory)[0].SwapLabelsAndTryCatchOperations(ref ins);
                    e.Emit(new(Pop));
                    patchedParts++;
                }
            }
            else if (MatchOpCodes(instructions, i, pattern33))
            {
                if (instructions[i + 1].Operand is MsilOperandInline<MethodBase> call && call.Value == updateAfterSimMethod)
                {
                    e.EmitProfilerStartName([
                        LoadLocal(4).SwapLabels(ref ins),
                        LoadField(debugNameField)
                    ]);
                    e.StoreLocal(timerLocal);
                    // Move past existing ops
                    e.Emit(ins);
                    e.Emit(instructions[++i]);
                    // Dispose timer
                    e.EmitDisposeProfilerTimer(timerLocal);
                    patchedParts++;
                    continue;
                }
            }
            else if (ins.OpCode == Ret)
            {
                break;
            }

            e.Emit(ins);
        }

        e.EmitStopProfiler()[0].CopyLabelsAndTryCatchOperations(instructions[^1]);
        e.EmitStopProfiler();
        e.Emit(new(Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Error($"Failed to patch {nameof(MySession)}.{nameof(MySession.UpdateComponents)}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    [MethodImpl(Inline)] static bool Prefix_SendVicinityInformation(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.SendVicinityInformation); return true; }
}
