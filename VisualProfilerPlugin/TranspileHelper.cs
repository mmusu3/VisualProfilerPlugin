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

    public static MsilInstruction LoadString(string str) => new MsilInstruction(OpCodes.Ldstr).InlineValue(str);
    public static MsilInstruction LoadField(FieldInfo field) => new MsilInstruction(OpCodes.Ldfld).InlineValue(field);
    public static MsilInstruction Call(MethodInfo method) => new MsilInstruction(OpCodes.Call).InlineValue(method);
    public static MsilInstruction CallVirt(MethodInfo method) => new MsilInstruction(OpCodes.Callvirt).InlineValue(method);
    public static MsilInstruction NewObj(ConstructorInfo ctor) => new MsilInstruction(OpCodes.Newobj).InlineValue(ctor);

    static class ProfilerMembers
    {
        internal static readonly ConstructorInfo KeyCtor = typeof(ProfilerKey).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(int)], null)!;
        internal static readonly ConstructorInfo ExtraDataLongCtor = typeof(ProfilerEvent.ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [typeof(long), typeof(string)], null)!;
        internal static readonly MethodInfo StartKeyExtraMethod = typeof(Profiler).GetPublicStaticMethod(nameof(Profiler.Start), [typeof(ProfilerKey), typeof(ProfilerTimerOptions), typeof(ProfilerEvent.ExtraData)])!;
        internal static readonly MethodInfo StopTimerMethod = typeof(ProfilerTimer).GetPublicInstanceMethod(nameof(ProfilerTimer.Stop))!;
    }

    public static void EmitProfilerStartLongExtra(List<MsilInstruction> instructions, ProfilerKey key, ProfilerTimerOptions timerOptions,
        string formatString, ReadOnlySpan<MsilInstruction> dataInstructions)
    {
        void Emit(MsilInstruction ins) => instructions.Add(ins);

        Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue(key.GlobalIndex));
        Emit(NewObj(ProfilerMembers.KeyCtor));
        Emit(new MsilInstruction(OpCodes.Ldc_I4).InlineValue((int)timerOptions));

        for (int i = 0; i < dataInstructions.Length; i++)
            Emit(dataInstructions[i]);

        Emit(LoadString(formatString));
        Emit(NewObj(ProfilerMembers.ExtraDataLongCtor));
        Emit(Call(ProfilerMembers.StartKeyExtraMethod));
    }

    public static void EmitStopProfilerTimer(List<MsilInstruction> instructions)
    {
        instructions.Add(Call(ProfilerMembers.StopTimerMethod));
    }

    public static void EmitStopProfilerTimer(List<MsilInstruction> instructions, MsilLocal timerLocal)
    {
        instructions.Add(timerLocal.AsValueLoad());
        instructions.Add(Call(ProfilerMembers.StopTimerMethod));
    }
}
