using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Torch.Managers.PatchManager.MSIL;

namespace VisualProfiler;

static class TranspileHelper
{
    static readonly FieldInfo instructionOffsetField = typeof(MsilInstruction).GetField("<Offset>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;

    static MsilInstruction CloneInstruction(MsilInstruction instruction)
    {
        var newI = instruction.CopyWith(instruction.OpCode);
        instructionOffsetField.SetValue(newI, instruction.Offset);

        return newI;
    }

    public static MsilInstruction CopyTryCatchOperations(this MsilInstruction instruction, MsilInstruction sourceInstruction)
    {
        instruction.TryCatchOperations.AddRange(sourceInstruction.TryCatchOperations);

        return instruction;
    }

    public static MsilInstruction CopyLabels(this MsilInstruction instruction, MsilInstruction sourceInstruction)
    {
        foreach (var label in sourceInstruction.Labels)
            instruction.Labels.Add(label);

        return instruction;
    }

    public static MsilInstruction CopyLabelsAndTryCatchOperations(this MsilInstruction instruction, MsilInstruction sourceInstruction)
    {
        foreach (var label in sourceInstruction.Labels)
            instruction.Labels.Add(label);

        instruction.TryCatchOperations.AddRange(sourceInstruction.TryCatchOperations);

        return instruction;
    }

    public static MsilInstruction SwapTryCatchOperations(this MsilInstruction instruction, ref MsilInstruction sourceInstruction)
    {
        instruction.TryCatchOperations.AddRange(sourceInstruction.TryCatchOperations);

        var newSource = CloneInstruction(sourceInstruction);
        newSource.TryCatchOperations.Clear();

        sourceInstruction = newSource;

        return instruction;
    }

    public static MsilInstruction SwapLabels(this MsilInstruction instruction, ref MsilInstruction sourceInstruction)
    {
        foreach (var label in sourceInstruction.Labels)
            instruction.Labels.Add(label);

        var newSource = CloneInstruction(sourceInstruction);
        newSource.Labels.Clear();

        sourceInstruction = newSource;

        return instruction;
    }

    public static MsilInstruction SwapLabelsAndTryCatchOperations(this MsilInstruction instruction, ref MsilInstruction sourceInstruction)
    {
        foreach (var label in sourceInstruction.Labels)
            instruction.Labels.Add(label);

        instruction.TryCatchOperations.AddRange(sourceInstruction.TryCatchOperations);

        var newSource = CloneInstruction(sourceInstruction);
        newSource.Labels.Clear();
        newSource.TryCatchOperations.Clear();

        sourceInstruction = newSource;

        return instruction;
    }

    public static bool MatchOpCodes(MsilInstruction[] instructions, int start, ReadOnlySpan<OpCode> opcodes)
    {
        if (instructions.Length < start + opcodes.Length)
            return false;

        for (int i = 0; i < opcodes.Length; i++)
        {
            if (instructions[start + i].OpCode != opcodes[i])
                return false;
        }

        return true;
    }

    public static MsilInstruction LoadConst(int value)
    {
        return value switch {
            -1 => new MsilInstruction(OpCodes.Ldc_I4_M1),
            0 => new MsilInstruction(OpCodes.Ldc_I4_0),
            1 => new MsilInstruction(OpCodes.Ldc_I4_1),
            2 => new MsilInstruction(OpCodes.Ldc_I4_2),
            3 => new MsilInstruction(OpCodes.Ldc_I4_3),
            4 => new MsilInstruction(OpCodes.Ldc_I4_4),
            5 => new MsilInstruction(OpCodes.Ldc_I4_5),
            6 => new MsilInstruction(OpCodes.Ldc_I4_6),
            7 => new MsilInstruction(OpCodes.Ldc_I4_7),
            8 => new MsilInstruction(OpCodes.Ldc_I4_8),
            _ => new MsilInstruction(OpCodes.Ldc_I4).InlineValue(value),
        };
    }

    public static MsilInstruction LoadString(string str) => new MsilInstruction(OpCodes.Ldstr).InlineValue(str);

    public static MsilInstruction LoadLocal(int localIndex)
    {
        return localIndex switch {
            0 => new MsilInstruction(OpCodes.Ldloc_0),
            1 => new MsilInstruction(OpCodes.Ldloc_1),
            2 => new MsilInstruction(OpCodes.Ldloc_2),
            3 => new MsilInstruction(OpCodes.Ldloc_3),
            < 256 => new MsilInstruction(OpCodes.Ldloc_S).InlineValue(new MsilLocal(localIndex)),
            _ => new MsilInstruction(OpCodes.Ldloc).InlineValue(new MsilLocal(localIndex)),
        };
    }

    public static MsilInstruction LoadLocal       (LocalVariableInfo local) => LoadLocal(local.LocalIndex);
    public static MsilInstruction LoadField       (FieldInfo field) => new MsilInstruction(OpCodes.Ldfld).InlineValue(field);
    public static MsilInstruction LoadFieldAddress(FieldInfo field) => new MsilInstruction(OpCodes.Ldflda).InlineValue(field);
    public static MsilInstruction LoadStaticField (FieldInfo field) => new MsilInstruction(OpCodes.Ldsfld).InlineValue(field);
    public static MsilInstruction Call            (MethodInfo method) => new MsilInstruction(OpCodes.Call).InlineValue(method);
    public static MsilInstruction CallVirt        (MethodInfo method) => new MsilInstruction(OpCodes.Callvirt).InlineValue(method);
    public static MsilInstruction NewObj          (ConstructorInfo ctor) => new MsilInstruction(OpCodes.Newobj).InlineValue(ctor);

    public static void Emit            (this List<MsilInstruction> instructions, MsilInstruction instruction) => instructions.Add(instruction);
    public static void LoadConst       (this List<MsilInstruction> instructions, int value) => instructions.Add(LoadConst(value));
    public static void LoadString      (this List<MsilInstruction> instructions, string str) => instructions.Add(LoadString(str));

    public static void LoadLocal(this List<MsilInstruction> instructions, MsilLocal local)
    {
        instructions.Add(local.Index switch {
            0 => new MsilInstruction(OpCodes.Ldloc_0),
            1 => new MsilInstruction(OpCodes.Ldloc_1),
            2 => new MsilInstruction(OpCodes.Ldloc_2),
            3 => new MsilInstruction(OpCodes.Ldloc_3),
            < 256 => new MsilInstruction(OpCodes.Ldloc_S).InlineValue(local),
            _ => new MsilInstruction(OpCodes.Ldloc).InlineValue(local),
        });
    }

    public static void LoadLocalAddress(this List<MsilInstruction> instructions, MsilLocal local) => instructions.Add(new MsilInstruction(local.Index < 256 ? OpCodes.Ldloca_S : OpCodes.Ldloca).InlineValue(local));
    public static void LoadLocal       (this List<MsilInstruction> instructions, int localIndex) => instructions.Add(LoadLocal(localIndex));
    public static void LoadLocal       (this List<MsilInstruction> instructions, LocalVariableInfo local) => instructions.Add(LoadLocal(local.LocalIndex));
    public static void LoadField       (this List<MsilInstruction> instructions, FieldInfo field) => instructions.Add(LoadField(field));
    public static void LoadFieldAddress(this List<MsilInstruction> instructions, FieldInfo field) => instructions.Add(LoadFieldAddress(field));
    public static void LoadStaticField (this List<MsilInstruction> instructions, FieldInfo field) => instructions.Add(LoadStaticField(field));
    public static void Call            (this List<MsilInstruction> instructions, MethodInfo method) => instructions.Add(Call(method));
    public static void CallVirt        (this List<MsilInstruction> instructions, MethodInfo method) => instructions.Add(CallVirt(method));
    public static void NewObj          (this List<MsilInstruction> instructions, ConstructorInfo ctor) => instructions.Add(NewObj(ctor));

    public static void StoreLocal(this List<MsilInstruction> instructions, MsilLocal local)
    {
        instructions.Add(local.Index switch {
            0 => new MsilInstruction(OpCodes.Stloc_0),
            1 => new MsilInstruction(OpCodes.Stloc_1),
            2 => new MsilInstruction(OpCodes.Stloc_2),
            3 => new MsilInstruction(OpCodes.Stloc_3),
            < 256 => new MsilInstruction(OpCodes.Stloc_S).InlineValue(local),
            _ => new MsilInstruction(OpCodes.Stloc).InlineValue(local),
        });
    }

    internal static class ProfilerMembers
    {
        internal static readonly ConstructorInfo KeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null)!;
        internal static readonly ConstructorInfo ExtraDataLongCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null)!;
        internal static readonly ConstructorInfo ExtraDataObjCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(object), typeof(string)], null)!;
        internal static readonly MethodInfo StartIndexExtraMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(int), typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        internal static readonly MethodInfo StartIndexMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(int), typeof(string)])!;
        internal static readonly MethodInfo StartKeyMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions)])!;
        internal static readonly MethodInfo StartKeyExtraMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        internal static readonly MethodInfo StartNameExtraMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        internal static readonly MethodInfo StartNameMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(string)])!;
        internal static readonly MethodInfo StartLiteMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.StartLite), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData).MakeByRefType()])!;
        internal static readonly MethodInfo RestartIndexMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Restart), [typeof(int), typeof(string), typeof(ProfilerTimerOptions)])!;
        internal static readonly MethodInfo StopTimerMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop))!;
        internal static readonly MethodInfo DisposeTimerMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Dispose))!;
        internal static readonly MethodInfo DisposeEventHandleMethod = typeof(ProfilerEventHandle).GetPublicInstanceMethod(nameof(ProfilerEventHandle.Dispose))!;
        internal static readonly MethodInfo StopMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Stop))!;
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStart(this List<MsilInstruction> instructions, int index, string name)
    {
        instructions.Add(LoadConst(index));
        instructions.Add(LoadString(name));
        instructions.Add(Call(ProfilerMembers.StartIndexMethod));

        return instructions.AsSpan()[^3..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStartObjExtra(this List<MsilInstruction> instructions, int index, string name,
        ProfilerTimerOptions timerOptions, string? formatString, ReadOnlySpan<MsilInstruction> dataInstructions)
    {
        instructions.Add(LoadConst(index));
        instructions.Add(LoadString(name));
        instructions.Add(LoadConst((int)timerOptions));

        for (int i = 0; i < dataInstructions.Length; i++)
            instructions.Add(dataInstructions[i]);

        instructions.Add(formatString != null ? LoadString(formatString) : new MsilInstruction(OpCodes.Ldnull));
        instructions.Add(NewObj(ProfilerMembers.ExtraDataObjCtor));
        instructions.Add(Call(ProfilerMembers.StartIndexExtraMethod));

        return instructions.AsSpan()[^(6 + dataInstructions.Length)..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStart(this List<MsilInstruction> instructions, string name)
    {
        instructions.Add(LoadString(name));
        instructions.Add(Call(ProfilerMembers.StartNameMethod));

        return instructions.AsSpan()[^2..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStartName(this List<MsilInstruction> instructions, ReadOnlySpan<MsilInstruction> nameInstructions)
    {
        for (int i = 0; i < nameInstructions.Length; i++)
            instructions.Add(nameInstructions[i]);

        instructions.Add(Call(ProfilerMembers.StartNameMethod));

        return instructions.AsSpan()[^(1 + nameInstructions.Length)..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStart(this List<MsilInstruction> instructions, ProfilerKey key, ProfilerTimerOptions timerOptions)
    {
        instructions.Add(LoadConst(key.GlobalIndex));
        instructions.Add(NewObj(ProfilerMembers.KeyCtor));
        instructions.Add(LoadConst((int)timerOptions));
        instructions.Add(Call(ProfilerMembers.StartKeyMethod));

        return instructions.AsSpan()[^4..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStartLongExtra(this List<MsilInstruction> instructions, ProfilerKey key, ProfilerTimerOptions timerOptions,
        string? formatString, ReadOnlySpan<MsilInstruction> dataInstructions)
    {
        instructions.Add(LoadConst(key.GlobalIndex));
        instructions.Add(NewObj(ProfilerMembers.KeyCtor));
        instructions.Add(LoadConst((int)timerOptions));

        for (int i = 0; i < dataInstructions.Length; i++)
            instructions.Add(dataInstructions[i]);

        instructions.Add(formatString != null ? LoadString(formatString) : new MsilInstruction(OpCodes.Ldnull));
        instructions.Add(NewObj(ProfilerMembers.ExtraDataLongCtor));
        instructions.Add(Call(ProfilerMembers.StartKeyExtraMethod));

        return instructions.AsSpan()[^(6 + dataInstructions.Length)..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStartObjExtra(this List<MsilInstruction> instructions, ProfilerKey key, ProfilerTimerOptions timerOptions,
        string? formatString, ReadOnlySpan<MsilInstruction> dataInstructions)
    {
        instructions.Add(LoadConst(key.GlobalIndex));
        instructions.Add(NewObj(ProfilerMembers.KeyCtor));
        instructions.Add(LoadConst((int)timerOptions));

        for (int i = 0; i < dataInstructions.Length; i++)
            instructions.Add(dataInstructions[i]);

        instructions.Add(formatString != null ? LoadString(formatString) : new MsilInstruction(OpCodes.Ldnull));
        instructions.Add(NewObj(ProfilerMembers.ExtraDataObjCtor));
        instructions.Add(Call(ProfilerMembers.StartKeyExtraMethod));

        return instructions.AsSpan()[^(6 + dataInstructions.Length)..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStartNameObjExtra(this List<MsilInstruction> instructions,
        ReadOnlySpan<MsilInstruction> nameInstructions, ProfilerTimerOptions timerOptions,
        string? formatString, ReadOnlySpan<MsilInstruction> dataInstructions)
    {
        for (int i = 0; i < nameInstructions.Length; i++)
            instructions.Add(nameInstructions[i]);

        instructions.Add(LoadConst((int)timerOptions));

        for (int i = 0; i < dataInstructions.Length; i++)
            instructions.Add(dataInstructions[i]);

        instructions.Add(formatString != null ? LoadString(formatString) : new MsilInstruction(OpCodes.Ldnull));
        instructions.Add(NewObj(ProfilerMembers.ExtraDataObjCtor));
        instructions.Add(Call(ProfilerMembers.StartNameExtraMethod));

        return instructions.AsSpan()[^(4 + nameInstructions.Length + dataInstructions.Length)..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerStartLiteObjExtra(this List<MsilInstruction> instructions, ProfilerKey key,
        ProfilerTimerOptions timerOptions, string? formatString, ReadOnlySpan<MsilInstruction> dataInstructions, MsilLocal dataLocal)
    {
        instructions.Add(LoadConst(key.GlobalIndex));
        instructions.Add(NewObj(ProfilerMembers.KeyCtor));
        instructions.Add(LoadConst((int)timerOptions));

        for (int i = 0; i < dataInstructions.Length; i++)
            instructions.Add(dataInstructions[i]);

        instructions.Add(formatString != null ? LoadString(formatString) : new MsilInstruction(OpCodes.Ldnull));
        instructions.Add(NewObj(ProfilerMembers.ExtraDataObjCtor));
        instructions.Add(dataLocal.AsValueStore());
        instructions.Add(dataLocal.AsReferenceLoad());
        instructions.Add(Call(ProfilerMembers.StartLiteMethod));

        return instructions.AsSpan()[^(8 + dataInstructions.Length)..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitProfilerRestart(this List<MsilInstruction> instructions, int index, string name, ProfilerTimerOptions timerOptions)
    {
        instructions.Add(LoadConst(index));
        instructions.Add(LoadString(name));
        instructions.Add(LoadConst((int)timerOptions));
        instructions.Add(Call(ProfilerMembers.RestartIndexMethod));

        return instructions.AsSpan()[^4..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitStopProfilerTimer(this List<MsilInstruction> instructions)
    {
        instructions.Add(Call(ProfilerMembers.StopTimerMethod));

        return instructions.AsSpan()[^1..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitStopProfilerTimer(this List<MsilInstruction> instructions, MsilLocal timerLocal)
    {
        instructions.Add(timerLocal.AsValueLoad());
        instructions.Add(Call(ProfilerMembers.StopTimerMethod));

        return instructions.AsSpan()[^2..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitDisposeProfilerTimer(this List<MsilInstruction> instructions)
    {
        instructions.Add(Call(ProfilerMembers.DisposeTimerMethod));

        return instructions.AsSpan()[^1..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitDisposeProfilerTimer(this List<MsilInstruction> instructions, MsilLocal timerLocal)
    {
        instructions.Add(timerLocal.AsValueLoad());
        instructions.Add(Call(ProfilerMembers.DisposeTimerMethod));

        return instructions.AsSpan()[^2..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitDisposeProfilerEventHandle(this List<MsilInstruction> instructions)
    {
        instructions.Add(Call(ProfilerMembers.DisposeEventHandleMethod));

        return instructions.AsSpan()[^1..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitDisposeProfilerEventHandle(this List<MsilInstruction> instructions, MsilLocal handleLocal)
    {
        instructions.Add(handleLocal.AsReferenceLoad());
        instructions.Add(Call(ProfilerMembers.DisposeEventHandleMethod));

        return instructions.AsSpan()[^2..];
    }

    public static ReadOnlySpan<MsilInstruction> EmitStopProfiler(this List<MsilInstruction> instructions)
    {
        instructions.Add(Call(ProfilerMembers.StopMethod));

        return instructions.AsSpan()[^1..];
    }
}
