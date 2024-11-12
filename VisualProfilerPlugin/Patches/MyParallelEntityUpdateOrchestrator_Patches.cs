using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ParallelTasks;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Library.Threading;
using VRage.Utils;
using static System.Reflection.Emit.OpCodes;
using static VisualProfiler.TranspileHelper;
using Instn = Torch.Managers.PatchManager.MSIL.MsilInstruction;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyParallelEntityUpdateOrchestrator_Patches
{
    static bool useLiteProfiling = true;

    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchOnceBeforeFrame), _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchBeforeSimulation), _public: true, _static: false);
        //PatchPrefixSuffixPair(ctx, "UpdateBeforeSimulation", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, "UpdateBeforeSimulation10", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, "UpdateBeforeSimulation100", _public: false, _static: false);
        Transpile(ctx, "UpdateBeforeSimulation", _public: false, _static: false);
        Transpile(ctx, "UpdateBeforeSimulation10", _public: false, _static: false);
        Transpile(ctx, "UpdateBeforeSimulation100", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchSimulate), _public: true, _static: false);
        Transpile(ctx, "DispatchSimulate", _public: true, _static: false);
        PatchPrefixSuffixPair(ctx, nameof(MyParallelEntityUpdateOrchestrator.DispatchAfterSimulation), _public: true, _static: false);
        //PatchPrefixSuffixPair(ctx, "UpdateAfterSimulation", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, "UpdateAfterSimulation10", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, "UpdateAfterSimulation100", _public: false, _static: false);
        Transpile(ctx, "UpdateAfterSimulation", _public: false, _static: false);
        Transpile(ctx, "UpdateAfterSimulation10", _public: false, _static: false);
        Transpile(ctx, "UpdateAfterSimulation100", _public: false, _static: false);
        //PatchPrefixSuffixPair(ctx, "PerformParallelUpdate", _public: false, _static: false);

        var source = typeof(MyParallelEntityUpdateOrchestrator).GetNonPublicInstanceMethod("PerformParallelUpdate");
        var prefix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod(nameof(Prefix_PerformParallelUpdate));
        ctx.GetPattern(source).Prefixes.Add(prefix);

        Transpile(ctx, "ParallelUpdateHandlerBeforeSimulation", _public: false, _static: false);
        Transpile(ctx, "ParallelUpdateHandlerAfterSimulation", _public: false, _static: false);

        //var deleg = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod(nameof(ParallelUpdateHandlerBeforeSimulation))
        //    .CreateDelegate(typeof(Action<IMyParallelUpdateable>));

        //typeof(MyParallelEntityUpdateOrchestrator).GetField("m_parallelUpdateHandlerBeforeSimulation", BindingFlags.NonPublic | BindingFlags.Instance)!
        //    .SetValue(MyEntities.Orchestrator, deleg);

        //deleg = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod(nameof(ParallelUpdateHandlerAfterSimulation))
        //    .CreateDelegate(typeof(Action<IMyParallelUpdateable>));

        //typeof(MyParallelEntityUpdateOrchestrator).GetField("m_parallelUpdateHandlerAfterSimulation", BindingFlags.NonPublic | BindingFlags.Instance)!
        //    .SetValue(MyEntities.Orchestrator, deleg);

        //source = typeof(MyParallelEntityUpdateOrchestrator).GetPublicInstanceMethod(nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater));
        //prefix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod(nameof(Prefix_ProcessInvokeLater));
        //ctx.GetPattern(source).Prefixes.Add(prefix);

        Transpile(ctx, nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyParallelEntityUpdateOrchestrator).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static void Transpile(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyParallelEntityUpdateOrchestrator).GetMethod(methodName, _public, _static);
        var transpiler = typeof(MyParallelEntityUpdateOrchestrator_Patches).GetNonPublicStaticMethod("Transpile_" + methodName);

        patchContext.GetPattern(source).Transpilers.Add(transpiler);
    }

    static class Keys
    {
        internal static ProfilerKey DispatchOnceBeforeFrame;
        internal static ProfilerKey DispatchBeforeSimulation;
        internal static ProfilerKey UpdateBeforeSimulation;
        internal static ProfilerKey UpdateBeforeSimulation10;
        internal static ProfilerKey UpdateBeforeSimulation100;
        internal static ProfilerKey DispatchSimulate;
        internal static ProfilerKey DispatchAfterSimulation;
        internal static ProfilerKey UpdateAfterSimulation;
        internal static ProfilerKey UpdateAfterSimulation10;
        internal static ProfilerKey UpdateAfterSimulation100;
        internal static ProfilerKey PerformParallelUpdate;
        internal static ProfilerKey ProcessInvokeLater;

        internal static ProfilerKey EntityUpdateBeforeSim;
        internal static ProfilerKey EntityUpdateBeforeSim10;
        internal static ProfilerKey EntityUpdateBeforeSim100;
        internal static ProfilerKey EntitySimulate;
        internal static ProfilerKey EntityUpdateAfterSim;
        internal static ProfilerKey EntityUpdateAfterSim10;
        internal static ProfilerKey EntityUpdateAfterSim100;

        internal static void Init()
        {
            DispatchOnceBeforeFrame = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchOnceBeforeFrame");
            DispatchBeforeSimulation = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchBeforeSimulation");
            UpdateBeforeSimulation = ProfilerKeyCache.GetOrAdd("UpdateBeforeSimulation");
            UpdateBeforeSimulation10 = ProfilerKeyCache.GetOrAdd("UpdateBeforeSimulation10");
            UpdateBeforeSimulation100 = ProfilerKeyCache.GetOrAdd("UpdateBeforeSimulation100");
            DispatchSimulate = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchSimulate");
            DispatchAfterSimulation = ProfilerKeyCache.GetOrAdd("MyParallelEntityUpdateOrchestrator.DispatchAfterSimulation");
            UpdateAfterSimulation = ProfilerKeyCache.GetOrAdd("UpdateAfterSimulation");
            UpdateAfterSimulation10 = ProfilerKeyCache.GetOrAdd("UpdateAfterSimulation10");
            UpdateAfterSimulation100 = ProfilerKeyCache.GetOrAdd("UpdateAfterSimulation100");
            PerformParallelUpdate = ProfilerKeyCache.GetOrAdd("PerformParallelUpdate");
            ProcessInvokeLater = ProfilerKeyCache.GetOrAdd("ProcessInvokeLater");

            EntityUpdateBeforeSim = ProfilerKeyCache.GetOrAdd("MyEntity.UpdateBeforeSimulation");
            EntityUpdateBeforeSim10 = ProfilerKeyCache.GetOrAdd("MyEntity.UpdateBeforeSimulation10");
            EntityUpdateBeforeSim100 = ProfilerKeyCache.GetOrAdd("MyEntity.UpdateBeforeSimulation100");
            EntitySimulate = ProfilerKeyCache.GetOrAdd("MyEntity.Simulate");
            EntityUpdateAfterSim = ProfilerKeyCache.GetOrAdd("MyEntity.UpdateAfterSimulation");
            EntityUpdateAfterSim10 = ProfilerKeyCache.GetOrAdd("MyEntity.UpdateAfterSimulation10");
            EntityUpdateAfterSim100 = ProfilerKeyCache.GetOrAdd("MyEntity.UpdateAfterSimulation100");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer)
    { __local_timer.Stop(); }

    #region Before

    [MethodImpl(Inline)] static bool Prefix_DispatchOnceBeforeFrame(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchOnceBeforeFrame); return true; }

    [MethodImpl(Inline)] static bool Prefix_DispatchBeforeSimulation(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchBeforeSimulation); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateBeforeSimulation(ref ProfilerTimer __local_timer, HashSet<MyEntity> __field_m_entitiesForUpdate)
    {
        __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdate.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateBeforeSimulation10(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation10, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdate10.Count + __field_m_entitiesForUpdate10Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateBeforeSimulation100(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateBeforeSimulation100, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdate100.Count + __field_m_entitiesForUpdate100Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    #endregion

    [MethodImpl(Inline)] static bool Prefix_DispatchSimulate(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchSimulate); return true; }

    static IEnumerable<Instn> Transpile_DispatchSimulate(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<Instn>((int)(instructions.Length * 1.1f));

        void Emit(Instn ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyParallelEntityUpdateOrchestrator)}.{nameof(MyParallelEntityUpdateOrchestrator.DispatchSimulate)}.");

        int expectedParts = 3;
        int patchedParts = 0;

        var keyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null)!;
        var extraDataCtor1 = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null)!;
        var extraDataCtor2 = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null)!;
        var startMethod1 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var startMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop))!;
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose))!;
        var startLiteMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.StartLite), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData).MakeByRefType()])!;
        var disposeMethod2 = typeof(ProfilerEventHandle).GetPublicInstanceMethod(nameof(ProfilerEventHandle.Dispose))!;

        var applyChangesMethod = typeof(MyParallelEntityUpdateOrchestrator).GetNonPublicInstanceMethod("ApplyChanges");
        var entitiesForSimulateField = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForSimulate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var getCountMethod = typeof(List<MyEntity>).GetProperty(nameof(List<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;
        var simulateMethod = typeof(MyEntity).GetPublicInstanceMethod(nameof(MyEntity.Simulate))!;
        var getTypeMethod = typeof(object).GetPublicInstanceMethod(nameof(GetType))!;
        var nameGetter = typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;
        var processInvokeLaterMethod = typeof(MyParallelEntityUpdateOrchestrator).GetPublicInstanceMethod(nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater));

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));

        MsilLocal timerLocal2;
        MsilLocal dataLocal;
        MsilLocal handleLocal;

        if (useLiteProfiling)
        {
            timerLocal2 = null!;
            dataLocal = __localCreator(typeof(ProfilerEvent.ExtraData));
            handleLocal = __localCreator(typeof(ProfilerEventHandle));
        }
        else
        {
            timerLocal2 = __localCreator(typeof(ProfilerTimer));
            dataLocal = null!;
            handleLocal = null!;
        }

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == Ldloc_1 && instructions[i + 1].OpCode == Callvirt
                && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call1 && call1.Value == simulateMethod)
            {
                if (useLiteProfiling)
                {
                    Emit(new Instn(Ldc_I4).InlineValue(Keys.EntitySimulate.GlobalIndex));
                    Emit(NewObj(keyCtor));
                    Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                    Emit(new Instn(ins.OpCode)); // entity
                    Emit(new Instn(Ldnull)); // data format
                    Emit(NewObj(extraDataCtor2));
                    Emit(dataLocal.AsValueStore());
                    Emit(dataLocal.AsReferenceLoad());
                    Emit(Call(startLiteMethod));
                    Emit(handleLocal.AsValueStore());

                    // Original call
                    Emit(ins);
                    Emit(instructions[++i]);

                    Emit(handleLocal.AsReferenceLoad());
                    Emit(Call(disposeMethod2));
                }
                else
                {
                    Emit(new Instn(ins.OpCode)); // entity
                    Emit(CallVirt(getTypeMethod));
                    Emit(CallVirt(nameGetter));
                    Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                    Emit(new Instn(ins.OpCode)); // entity
                    Emit(new Instn(Ldnull)); // data format
                    Emit(NewObj(extraDataCtor2));
                    Emit(Call(startMethod2));
                    Emit(timerLocal2.AsValueStore());

                    // Original call
                    Emit(ins);
                    Emit(instructions[++i]);

                    Emit(timerLocal2.AsValueLoad());
                    Emit(Call(disposeMethod));
                }

                patchedParts++;
                continue;
            }
            else if (ins.OpCode == Ldarg_0 && instructions[i + 1].OpCode == OpCodes.Call
                && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call2 && call2.Value == processInvokeLaterMethod)
            {
                Emit(timerLocal1.AsValueLoad());
                Emit(Call(stopMethod));
                patchedParts++;
            }

            Emit(ins);

            if (i > 0 && instructions[i - 1].OpCode == Ldarg_0 && ins.OpCode == OpCodes.Call
                && ins.Operand is MsilOperandInline<MethodBase> call3 && call3.Value == applyChangesMethod)
            {
                Emit(new Instn(Ldc_I4).InlineValue(Keys.DispatchSimulate.GlobalIndex));
                Emit(NewObj(keyCtor));
                Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                Emit(new Instn(Ldarg_0));
                Emit(LoadField(entitiesForSimulateField));
                Emit(CallVirt(getCountMethod));
                Emit(new Instn(Conv_I8));
                Emit(LoadString("Num entities: {0:n0}"));
                Emit(NewObj(extraDataCtor1));
                Emit(Call(startMethod1));
                Emit(timerLocal1.AsValueStore());
                patchedParts++;
            }
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyParallelEntityUpdateOrchestrator)}.{nameof(MyParallelEntityUpdateOrchestrator.DispatchSimulate)}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    #region After

    [MethodImpl(Inline)] static bool Prefix_DispatchAfterSimulation(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DispatchAfterSimulation); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer,
        HashSet<MyEntity> __field_m_entitiesForUpdate, HashSet<MyEntity> __field_m_entitiesForUpdateAfter)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdate.Count + __field_m_entitiesForUpdateAfter.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateAfterSimulation10(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate10Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation10, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdate10.Count + __field_m_entitiesForUpdate10Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    [MethodImpl(Inline)]
    static bool Prefix_UpdateAfterSimulation100(ref ProfilerTimer __local_timer,
        MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100, MyDistributedTypeUpdater<MyEntity> __field_m_entitiesForUpdate100Heavy)
    {
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation100, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdate100.Count + __field_m_entitiesForUpdate100Heavy.Count, "Num entities: {0:n0}"));

        return true;
    }

    #endregion

    static IEnumerable<Instn> Transpile_UpdateBeforeSimulation(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var field = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var countGetter = typeof(HashSet<MyEntity>).GetProperty(nameof(HashSet<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var dataInstructions = new[] {
            new Instn(Ldarg_0),
            LoadField(field),
            CallVirt(countGetter),
            new Instn(Conv_I8)
        };

        return Transpile_Update(instructionStream, __localCreator, "UpdateBeforeSimulation", Keys.UpdateBeforeSimulation, Keys.EntityUpdateBeforeSim, dataInstructions, numParts: 1);
    }

    static IEnumerable<Instn> Transpile_UpdateBeforeSimulation10(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var field1 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate10", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var field2 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate10Heavy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var countGetter = typeof(MyDistributedTypeUpdater<MyEntity>).GetProperty(nameof(MyDistributedTypeUpdater<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var dataInstructions = new[] {
            new Instn(Ldarg_0),
            LoadField(field1),
            CallVirt(countGetter),
            new Instn(Ldarg_0),
            LoadField(field2),
            CallVirt(countGetter),
            new Instn(Add),
            new Instn(Conv_I8)
        };

        return Transpile_Update(instructionStream, __localCreator, "UpdateBeforeSimulation10", Keys.UpdateBeforeSimulation10, Keys.EntityUpdateBeforeSim10, dataInstructions);
    }

    static IEnumerable<Instn> Transpile_UpdateBeforeSimulation100(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var field1 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate100", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var field2 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate100Heavy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var countGetter = typeof(MyDistributedTypeUpdater<MyEntity>).GetProperty(nameof(MyDistributedTypeUpdater<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var dataInstructions = new[] {
            new Instn(Ldarg_0),
            LoadField(field1),
            CallVirt(countGetter),
            new Instn(Ldarg_0),
            LoadField(field2),
            CallVirt(countGetter),
            new Instn(Add),
            new Instn(Conv_I8)
        };

        return Transpile_Update(instructionStream, __localCreator, "UpdateBeforeSimulation100", Keys.UpdateBeforeSimulation100, Keys.EntityUpdateBeforeSim100, dataInstructions);
    }

    static IEnumerable<Instn> Transpile_UpdateAfterSimulation(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var field1 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var field2 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdateAfter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var countGetter = typeof(HashSet<MyEntity>).GetProperty(nameof(HashSet<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var dataInstructions = new[] {
            new Instn(Ldarg_0),
            LoadField(field1),
            CallVirt(countGetter),
            new Instn(Ldarg_0),
            LoadField(field2),
            CallVirt(countGetter),
            new Instn(Add),
            new Instn(Conv_I8)
        };

        return Transpile_Update(instructionStream, __localCreator, "UpdateAfterSimulation", Keys.UpdateAfterSimulation, Keys.EntityUpdateAfterSim, dataInstructions);
    }

    static IEnumerable<Instn> Transpile_UpdateAfterSimulation10(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var field1 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate10", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var field2 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate10Heavy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var countGetter = typeof(MyDistributedTypeUpdater<MyEntity>).GetProperty(nameof(MyDistributedTypeUpdater<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var dataInstructions = new[] {
            new Instn(Ldarg_0),
            LoadField(field1),
            CallVirt(countGetter),
            new Instn(Ldarg_0),
            LoadField(field2),
            CallVirt(countGetter),
            new Instn(Add),
            new Instn(Conv_I8)
        };

        return Transpile_Update(instructionStream, __localCreator, "UpdateAfterSimulation10", Keys.UpdateAfterSimulation10, Keys.EntityUpdateAfterSim10, dataInstructions);
    }

    static IEnumerable<Instn> Transpile_UpdateAfterSimulation100(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var field1 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate100", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var field2 = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_entitiesForUpdate100Heavy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var countGetter = typeof(MyDistributedTypeUpdater<MyEntity>).GetProperty(nameof(MyDistributedTypeUpdater<MyEntity>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var dataInstructions = new[] {
            new Instn(Ldarg_0),
            LoadField(field1),
            CallVirt(countGetter),
            new Instn(Ldarg_0),
            LoadField(field2),
            CallVirt(countGetter),
            new Instn(Add),
            new Instn(Conv_I8)
        };

        return Transpile_Update(instructionStream, __localCreator, "UpdateAfterSimulation100", Keys.UpdateAfterSimulation100, Keys.EntityUpdateAfterSim100, dataInstructions);
    }

    static IEnumerable<Instn> Transpile_Update(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator,
        string methodName, ProfilerKey key, ProfilerKey key2, Instn[] dataInstructions, int numParts = 2)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<Instn>((int)(instructions.Length * 1.1f));

        void Emit(Instn ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyParallelEntityUpdateOrchestrator)}.{methodName}.");

        int expectedParts = numParts;
        int patchedParts = 0;

        var keyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null)!;
        var extraDataCtor1 = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null)!;
        var extraDataCtor2 = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null)!;
        var startMethod1 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var startMethod2 = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop))!;
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose))!;
        var startLiteMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.StartLite), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData).MakeByRefType()])!;
        var disposeMethod2 = typeof(ProfilerEventHandle).GetPublicInstanceMethod(nameof(ProfilerEventHandle.Dispose))!;

        var updateMethod = typeof(MyEntity).GetPublicInstanceMethod(methodName)!;
        var getTypeMethod = typeof(object).GetPublicInstanceMethod(nameof(GetType))!;
        var nameGetter = typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));

        MsilLocal timerLocal2;
        MsilLocal dataLocal;
        MsilLocal handleLocal;

        if (useLiteProfiling)
        {
            timerLocal2 = null!;
            dataLocal = __localCreator(typeof(ProfilerEvent.ExtraData));
            handleLocal = __localCreator(typeof(ProfilerEventHandle));
        }
        else
        {
            timerLocal2 = __localCreator(typeof(ProfilerTimer));
            dataLocal = null!;
            handleLocal = null!;
        }

        Emit(new Instn(Ldc_I4).InlineValue(key.GlobalIndex));
        Emit(NewObj(keyCtor));
        Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
        newInstructions.AddRange(dataInstructions);
        Emit(LoadString("Num entities: {0:n0}"));
        Emit(NewObj(extraDataCtor1));
        Emit(Call(startMethod1));
        Emit(timerLocal1.AsValueStore());

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if ((ins.OpCode == Ldloc_1 || (numParts > 1 && ins.OpCode == Ldloc_2)) && instructions[i + 1].OpCode == Callvirt
                && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call && call.Value == updateMethod)
            {
                if (useLiteProfiling)
                {
                    Emit(new Instn(Ldc_I4).InlineValue(key2.GlobalIndex));
                    Emit(NewObj(keyCtor));
                    Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                    Emit(new Instn(ins.OpCode)); // entity
                    Emit(new Instn(Ldnull)); // data format
                    Emit(NewObj(extraDataCtor2));
                    Emit(dataLocal.AsValueStore());
                    Emit(dataLocal.AsReferenceLoad());
                    Emit(Call(startLiteMethod));
                    Emit(handleLocal.AsValueStore());

                    // Original call
                    Emit(ins);
                    Emit(instructions[++i]);

                    Emit(handleLocal.AsReferenceLoad());
                    Emit(Call(disposeMethod2));
                }
                else
                {
                    Emit(new Instn(ins.OpCode)); // entity
                    Emit(CallVirt(getTypeMethod));
                    Emit(CallVirt(nameGetter));
                    Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                    Emit(new Instn(ins.OpCode)); // entity
                    Emit(new Instn(Ldnull)); // data format
                    Emit(NewObj(extraDataCtor2));
                    Emit(Call(startMethod2));
                    Emit(timerLocal2.AsValueStore());

                    // Original call
                    Emit(ins);
                    Emit(instructions[++i]);

                    Emit(timerLocal2.AsValueLoad());
                    Emit(Call(disposeMethod));
                }

                patchedParts++;
                continue;
            }
            else if (ins.OpCode == Ret)
            {
                break;
            }

            Emit(ins);
        }

        Emit(timerLocal1.AsValueLoad().CopyLabelsAndTryCatchOperations(instructions[^1]));
        Emit(Call(stopMethod));
        Emit(new Instn(Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyParallelEntityUpdateOrchestrator)}.{methodName}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    //[MethodImpl(Inline)]
    //static bool Prefix_PerformParallelUpdate(ref ProfilerTimer __local_timer,
    //    HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelFirst, HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelLast)
    //{
    //    __local_timer = Profiler.Start("PerformParallelUpdate", ProfilerTimerOptions.ProfileMemory,
    //        new(__field_m_entitiesForUpdateParallelFirst.Count + __field_m_entitiesForUpdateParallelLast.Count, "Num entities: {0:n0}"));

    //    return true;
    //}

    // There is some weird issue with using prefix + suffix that causes a long pause at
    // the end of the function so this is used instead as a workaround.
    //
    static bool Prefix_PerformParallelUpdate(Action<IMyParallelUpdateable> updateFunction, IEnumerable<IMyParallelUpdateable> __field_m_helper,
        HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelFirst, HashSet<IMyParallelUpdateable> __field_m_entitiesForUpdateParallelLast)
    {
        using var stateToken = Havok.HkAccessControl.PushState(Havok.HkAccessControl.AccessState.SharedRead);

        using (Profiler.Start(Keys.PerformParallelUpdate, ProfilerTimerOptions.ProfileMemory,
            new(__field_m_entitiesForUpdateParallelFirst.Count + __field_m_entitiesForUpdateParallelLast.Count, "Num entities: {0:n0}")))
        {
            if (MyParallelEntityUpdateOrchestrator.ForceSerialUpdate)
            {
                foreach (var updatable in __field_m_helper)
                    updateFunction(updatable);
            }
            else
            {
                using (MyEntities.StartAsyncUpdateBlock())
                    Parallel.ForEach(__field_m_helper, updateFunction, MyParallelEntityUpdateOrchestrator.WorkerPriority, blocking: true);
            }
        }

        return false;
    }

    static void ParallelUpdateHandlerBeforeSimulation(IMyParallelUpdateable entity)
    {
        if (entity.MarkedForClose || (entity.UpdateFlags & MyParallelUpdateFlags.EACH_FRAME_PARALLEL) == 0 || !entity.InScene)
            return;

        using (Profiler.Start(entity.GetType().Name, ProfilerTimerOptions.ProfileMemory, extraData: new(entity)))
            entity.UpdateBeforeSimulationParallel();
    }

    static void ParallelUpdateHandlerAfterSimulation(IMyParallelUpdateable entity)
    {
        if (entity.MarkedForClose || (entity.UpdateFlags & MyParallelUpdateFlags.EACH_FRAME_PARALLEL) == 0 || !entity.InScene)
            return;

        using (Profiler.Start(entity.GetType().Name, ProfilerTimerOptions.ProfileMemory, extraData: new(entity)))
            entity.UpdateAfterSimulationParallel();
    }

    static IEnumerable<Instn> Transpile_ParallelUpdateHandlerBeforeSimulation(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<Instn>((int)(instructions.Length * 1.1f));

        void Emit(Instn ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyParallelEntityUpdateOrchestrator)}.ParallelUpdateHandlerBeforeSimulation.");

        const int expectedParts = 1;
        int patchedParts = 0;

        var extraDataCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null)!;
        var startMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose))!;

        var updateMethod = typeof(IMyParallelUpdateable).GetPublicInstanceMethod(nameof(IMyParallelUpdateable.UpdateBeforeSimulationParallel))!;
        var getTypeMethod = typeof(object).GetPublicInstanceMethod(nameof(GetType))!;
        var nameGetter = typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == Ldarg_1 && instructions[i + 1].OpCode == Callvirt
                && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call && call.Value == updateMethod)
            {
                Emit(new Instn(Ldarg_1)); // entity
                Emit(CallVirt(getTypeMethod));
                Emit(CallVirt(nameGetter));
                Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                Emit(new Instn(Ldarg_1)); // entity
                Emit(new Instn(Ldnull));
                Emit(NewObj(extraDataCtor));
                Emit(Call(startMethod));
                Emit(timerLocal.AsValueStore());
                Emit(ins);
                Emit(instructions[i + 1]);
                i++;
                Emit(timerLocal.AsValueLoad());
                Emit(Call(disposeMethod));
                patchedParts++;
                continue;
            }

            Emit(ins);
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyParallelEntityUpdateOrchestrator)}.ParallelUpdateHandlerBeforeSimulation. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    static IEnumerable<Instn> Transpile_ParallelUpdateHandlerAfterSimulation(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<Instn>((int)(instructions.Length * 1.1f));

        void Emit(Instn ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyParallelEntityUpdateOrchestrator)}.ParallelUpdateHandlerAfterSimulation.");

        const int expectedParts = 1;
        int patchedParts = 0;

        var extraDataCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null)!;
        var startMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose))!;

        var updateMethod = typeof(IMyParallelUpdateable).GetPublicInstanceMethod(nameof(IMyParallelUpdateable.UpdateAfterSimulationParallel))!;
        var getTypeMethod = typeof(object).GetPublicInstanceMethod(nameof(GetType))!;
        var nameGetter = typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;

        var timerLocal = __localCreator(typeof(ProfilerTimer));

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (ins.OpCode == Ldarg_1 && instructions[i + 1].OpCode == Callvirt
                && instructions[i + 1].Operand is MsilOperandInline<MethodBase> call && call.Value == updateMethod)
            {
                Emit(new Instn(Ldarg_1)); // entity
                Emit(CallVirt(getTypeMethod));
                Emit(CallVirt(nameGetter));
                Emit(new Instn(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                Emit(new Instn(Ldarg_1)); // entity
                Emit(new Instn(Ldnull));
                Emit(NewObj(extraDataCtor));
                Emit(Call(startMethod));
                Emit(timerLocal.AsValueStore());
                Emit(ins);
                Emit(instructions[i + 1]);
                i++;
                Emit(timerLocal.AsValueLoad());
                Emit(Call(disposeMethod));
                patchedParts++;
                continue;
            }

            Emit(ins);
        }

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyParallelEntityUpdateOrchestrator)}.ParallelUpdateHandlerAfterSimulation. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }

    [MethodImpl(Inline)]
    static bool Prefix_ProcessInvokeLater(ISharedCriticalSection __field_m_lockInvokeLater,
        ConcurrentQueue<(Action Callback, string DebugName)> __field_m_callbacksPendingExecution,
        ConcurrentQueue<(Action Callback, string DebugName)> __field_m_callbacksPendingExecutionSwap)
    {
        if (__field_m_callbacksPendingExecution.IsEmpty)
            return false;

        using var t = Profiler.Start(Keys.ProcessInvokeLater, ProfilerTimerOptions.ProfileMemory, new(__field_m_callbacksPendingExecution.Count));

        using (__field_m_lockInvokeLater.EnterUnique())
            MyUtils.Swap(ref __field_m_callbacksPendingExecution, ref __field_m_callbacksPendingExecutionSwap);

        foreach (var item in __field_m_callbacksPendingExecutionSwap)
        {
            using (Profiler.Start(item.DebugName ?? item.Callback.Method.Name))
                item.Callback();
        }

        while (__field_m_callbacksPendingExecutionSwap.TryDequeue(out _)) { }

        return false;
    }

    static IEnumerable<Instn> Transpile_ProcessInvokeLater(IEnumerable<Instn> instructionStream, Func<Type, MsilLocal> __localCreator)
    {
        var instructions = instructionStream.ToArray();
        var newInstructions = new List<Instn>((int)(instructions.Length * 1.1f));

        void Emit(Instn ins) => newInstructions.Add(ins);

        Plugin.Log.Debug($"Patching {nameof(MyParallelEntityUpdateOrchestrator)}.{nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater)}.");

        const int expectedParts = 2;
        int patchedParts = 0;

        var keyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null)!;
        var extraDataLongCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null)!;
        var extraDataObjCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null)!;
        var startKeyMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var startStringMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        var disposeMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose))!;
        var stopMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop))!;

        var lockField = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_lockInvokeLater", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var callbacksPendingField = typeof(MyParallelEntityUpdateOrchestrator).GetField("m_callbacksPendingExecution", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var getCountMethod = typeof(ConcurrentQueue<(Action, string)>).GetProperty(nameof(ConcurrentQueue<(Action, string)>.Count), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;
        var getMethodMethod = typeof(Action).GetProperty(nameof(Action.Method))!.GetMethod!;
        var nameGetter = typeof(MemberInfo).GetProperty(nameof(MemberInfo.Name), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!;
        var invokeMethod = typeof(Action).GetPublicInstanceMethod(nameof(Action.Invoke));

        var timerLocal1 = __localCreator(typeof(ProfilerTimer));
        var timerLocal2 = __localCreator(typeof(ProfilerTimer));
        var actionLocal = __localCreator(typeof(Action));

        ReadOnlySpan<OpCode> pattern1 = [Ret, Ldarg_0, Ldfld];

        for (int i = 0; i < instructions.Length; i++)
        {
            var ins = instructions[i];

            if (MatchOpCodes(instructions, i, pattern1)
                && instructions[i + 2].Operand is MsilOperandInline<FieldInfo> ldField && ldField.Value == lockField)
            {
                Emit(ins);
                ins = instructions[++i];
                Emit(new Instn(Ldc_I4).InlineValue(Keys.ProcessInvokeLater.GlobalIndex).SwapLabels(ref ins));
                Emit(NewObj(keyCtor));
                Emit(new(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                Emit(new(Ldarg_0));
                Emit(LoadField(callbacksPendingField));
                Emit(Call(getCountMethod));
                Emit(new(Conv_I8));
                Emit(new(Ldnull));
                Emit(NewObj(extraDataLongCtor));
                Emit(Call(startKeyMethod));
                Emit(timerLocal1.AsValueStore());
                patchedParts++;
            }
            else if (ins.OpCode == Callvirt && ins.Operand is MsilOperandInline<MethodBase> call && call.Value == invokeMethod)
            {
                Emit(new(Dup));
                Emit(actionLocal.AsValueStore());
                Emit(CallVirt(getMethodMethod));
                Emit(CallVirt(nameGetter));
                Emit(new(Ldc_I4_1)); // ProfilerTimerOptions.ProfileMemory
                Emit(actionLocal.AsValueLoad());
                Emit(new(Ldnull));
                Emit(NewObj(extraDataObjCtor));
                Emit(Call(startStringMethod));
                Emit(timerLocal2.AsValueStore());
                Emit(actionLocal.AsValueLoad());
                Emit(ins);
                Emit(timerLocal2.AsValueLoad());
                Emit(Call(disposeMethod));
                patchedParts++;
                continue;
            }
            else if (ins.OpCode == Ret && i == instructions.Length - 1)
            {
                break;
            }

            Emit(ins);
        }

        Emit(timerLocal1.AsValueLoad());
        Emit(Call(stopMethod));
        Emit(new(Ret));

        if (patchedParts != expectedParts)
        {
            Plugin.Log.Fatal($"Failed to patch {nameof(MyParallelEntityUpdateOrchestrator)}.{nameof(MyParallelEntityUpdateOrchestrator.ProcessInvokeLater)}. {patchedParts} out of {expectedParts} code parts matched.");
            return instructions;
        }
        else
        {
            Plugin.Log.Debug("Patch successful.");
            return newInstructions;
        }
    }
}
