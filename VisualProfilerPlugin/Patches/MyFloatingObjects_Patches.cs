using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyFloatingObjects_Patches
{
    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyFloatingObjects.UpdateAfterSimulation), _public: true, _static: false);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyFloatingObjects).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyFloatingObjects_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyFloatingObjects_Patches).GetNonPublicStaticMethod(nameof(Suffix));

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey UpdateAfterSimulation;

        internal static void Init()
        {
            UpdateAfterSimulation = ProfilerKeyCache.GetOrAdd("MyFloatingObjects.UpdateAfterSimulation");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Suffix(ref ProfilerTimer __local_timer) { __local_timer.Stop(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool Prefix_UpdateAfterSimulation(ref ProfilerTimer __local_timer/*, SortedSet<MyFloatingObject> __field_m_floatingOres,*/
        /*SortedSet<MyFloatingObject> __field_m_floatingItems, SortedSet<MyCargoContainerInventoryBagEntity> __field_m_floatingBags*/)
    {
        //int itemCount = __field_m_floatingOres.Count + __field_m_floatingItems.Count + __field_m_floatingBags.Count;

        //__local_timer = Profiler.Start(Keys.UpdateAfterSimulation, ProfilerTimerOptions.ProfileMemory, new(itemCount, "Total Items: {0:N}"));
        __local_timer = Profiler.Start(Keys.UpdateAfterSimulation, ProfilerTimerOptions.ProfileMemory, new(ProfilerEvent.EventCategory.FloatingObjects));

        return true;
    }
}
