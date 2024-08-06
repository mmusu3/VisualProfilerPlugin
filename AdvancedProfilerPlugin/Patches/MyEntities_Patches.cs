using System;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.ObjectBuilders;

namespace AdvancedProfiler.Patches;

[PatchShim]
static class MyEntities_Patches
{
    public static void Patch(PatchContext ctx)
    {
        var source = typeof(MyEntities).GetPublicStaticMethod("Load");
        var prefix = typeof(MyEntities_Patches).GetNonPublicStaticMethod(nameof(Prefix_Load));
        var suffix = typeof(MyEntities_Patches).GetNonPublicStaticMethod(nameof(Suffix_Load));

        var pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyEntities).GetPublicStaticMethod("CreateFromObjectBuilder");
        prefix = typeof(MyEntities_Patches).GetNonPublicStaticMethod(nameof(Prefix_CreateFromObjectBuilder));
        suffix = typeof(MyEntities_Patches).GetNonPublicStaticMethod(nameof(Suffix_CreateFromObjectBuilder));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);

        source = typeof(MyEntities).GetNonPublicStaticMethod("LoadEntity");
        prefix = typeof(MyEntities_Patches).GetNonPublicStaticMethod(nameof(Prefix_LoadEntity));
        suffix = typeof(MyEntities_Patches).GetNonPublicStaticMethod(nameof(Suffix_LoadEntity));

        pattern = ctx.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_Load(ref ProfilerTimer __local_timer)
    {
        __local_timer = Profiler.Start("MyEntities.Load");
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_Load(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_CreateFromObjectBuilder(ref ProfilerTimer __local_timer, MyObjectBuilder_EntityBase objectBuilder)
    {
        __local_timer = Profiler.Start("MyEntities.CreateFromObjectBuilder", profileMemory: true, new((Type)objectBuilder.TypeId, "Type: {0}"));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_CreateFromObjectBuilder(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_LoadEntity(ref ProfilerTimer __local_timer, MyObjectBuilder_EntityBase objectBuilder)
    {
        __local_timer = Profiler.Start("MyEntities.LoadEntity", profileMemory: true, new((Type)objectBuilder.TypeId, "Type: {0}"));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix_LoadEntity(ref ProfilerTimer __local_timer)
    {
        __local_timer.Stop();
    }
}
