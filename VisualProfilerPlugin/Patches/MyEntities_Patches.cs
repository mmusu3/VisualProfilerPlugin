using System;
using System.Runtime.CompilerServices;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;
using VRage.ObjectBuilders;

namespace VisualProfiler.Patches;

[PatchShim]
static class MyEntities_Patches
{
#if !NET9_0_OR_GREATER
    [Torch.Utils.ReflectedGetter(Name = "_invocationList", Type = typeof(MulticastDelegate))]
    static Func<MulticastDelegate, object> InvocationListGetter = null!;

    [Torch.Utils.ReflectedGetter(Name = "_invocationCount", Type = typeof(MulticastDelegate))]
    static Func<MulticastDelegate, IntPtr> InvocationCountGetter = null!;
#endif

    public static void Patch(PatchContext ctx)
    {
        Keys.Init();

        PatchPrefixSuffixPair(ctx, nameof(MyEntities.Load),                     _public: true,  _static: true);
        PatchPrefixSuffixPair(ctx, "LoadEntity",                                _public: false, _static: true);
        PatchPrefixSuffixPair(ctx, nameof(MyEntities.CreateFromObjectBuilder),  _public: true,  _static: true);
        PatchPrefixSuffixPair(ctx, nameof(MyEntities.Add),                      _public: true,  _static: true);
        PatchPrefixSuffixPair(ctx, nameof(MyEntities.RaiseEntityAdd),           _public: true,  _static: true);
        PatchPrefixSuffixPair(ctx, nameof(MyEntities.DeleteRememberedEntities), _public: true,  _static: true);
        PatchPrefixSuffixPair(ctx, "Save",                                      _public: false, _static: true);
    }

    static void PatchPrefixSuffixPair(PatchContext patchContext, string methodName, bool _public, bool _static)
    {
        var source = typeof(MyEntities).GetMethod(methodName, _public, _static);
        var prefix = typeof(MyEntities_Patches).GetNonPublicStaticMethod("Prefix_" + methodName);
        var suffix = typeof(MyEntities_Patches).GetNonPublicStaticMethod("Suffix");

        var pattern = patchContext.GetPattern(source);
        pattern.Prefixes.Add(prefix);
        pattern.Suffixes.Add(suffix);
    }

    static class Keys
    {
        internal static ProfilerKey Load;
        internal static ProfilerKey CreateFromObjectBuilder;
        internal static ProfilerKey LoadEntity;
        internal static ProfilerKey Add;
        internal static ProfilerKey RaiseEntityAdd;
        internal static ProfilerKey DeleteRememberedEntities;
        internal static ProfilerKey Save;

        internal static void Init()
        {
            Load                     = ProfilerKeyCache.GetOrAdd("MyEntities.Load");
            CreateFromObjectBuilder  = ProfilerKeyCache.GetOrAdd("MyEntities.CreateFromObjectBuilder");
            LoadEntity               = ProfilerKeyCache.GetOrAdd("MyEntities.LoadEntity");
            Add                      = ProfilerKeyCache.GetOrAdd("MyEntities.Add");
            RaiseEntityAdd           = ProfilerKeyCache.GetOrAdd("MyEntities.RaiseEntityAdd");
            DeleteRememberedEntities = ProfilerKeyCache.GetOrAdd("MyEntities.DeleteRememberedEntities");
            Save                     = ProfilerKeyCache.GetOrAdd("MyEntities.Save");
        }
    }

    const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

    [MethodImpl(Inline)] static void Suffix(ref ProfilerTimer __local_timer) => __local_timer.Stop();

    [MethodImpl(Inline)] static bool Prefix_Load(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Load); return true; }

    [MethodImpl(Inline)] static bool Prefix_CreateFromObjectBuilder(ref ProfilerTimer __local_timer, MyObjectBuilder_EntityBase objectBuilder)
    { __local_timer = Profiler.Start(Keys.CreateFromObjectBuilder, ProfilerTimerOptions.ProfileMemory, new((Type)objectBuilder.TypeId, "Type: {0}")); return true; }

    [MethodImpl(Inline)] static bool Prefix_LoadEntity(ref ProfilerTimer __local_timer, MyObjectBuilder_EntityBase objectBuilder)
    { __local_timer = Profiler.Start(Keys.LoadEntity, ProfilerTimerOptions.ProfileMemory, new((Type)objectBuilder.TypeId, "Type: {0}")); return true; }

    [MethodImpl(Inline)] static bool Prefix_Add(ref ProfilerTimer __local_timer, MyEntity entity)
    { __local_timer = Profiler.Start(Keys.Add, ProfilerTimerOptions.ProfileMemory, new(entity, "{0}")); return true; }

    [MethodImpl(Inline)]
    static bool Prefix_RaiseEntityAdd(ref ProfilerTimer __local_timer, MyEntity entity, Action<MyEntity> __field_OnEntityAdd)
    {
        __local_timer = Profiler.Start(Keys.RaiseEntityAdd, ProfilerTimerOptions.ProfileMemory, new(entity, "{0}"));

#if NET9_0_OR_GREATER
        foreach (var action in Delegate.EnumerateInvocationList(__field_OnEntityAdd))
        {
            using (Profiler.Start(action.Method.Name, ProfilerTimerOptions.ProfileMemory, new(action.Target?.GetType() ?? action.Method.DeclaringType)))
                action.Invoke(entity);
        }
#else
        var obj = InvocationListGetter(__field_OnEntityAdd);
        int count = (int)InvocationCountGetter(__field_OnEntityAdd);

        switch (obj)
        {
        case object[] list:
            {
                for (int i = 0; i < count; i++)
                {
                    var di = (Delegate)list[i];

                    using (Profiler.Start(di.Method.Name, ProfilerTimerOptions.ProfileMemory, new(di.Target?.GetType() ?? di.Method.DeclaringType)))
                        ((Action<MyEntity>)di).Invoke(entity);
                }

                break;
            }
        case Delegate dg:
            {
                using (Profiler.Start(dg.Method.Name, ProfilerTimerOptions.ProfileMemory, new(dg.Target?.GetType() ?? dg.Method.DeclaringType)))
                    ((Action<MyEntity>)dg).Invoke(entity);

                break;
            }
        default:
            {
                using (Profiler.Start(__field_OnEntityAdd.Method.Name, ProfilerTimerOptions.ProfileMemory, new(__field_OnEntityAdd.Target?.GetType() ?? __field_OnEntityAdd.Method.DeclaringType)))
                    __field_OnEntityAdd.Invoke(entity);

                break;
            }
        }
#endif

        return false;
    }

    [MethodImpl(Inline)] static bool Prefix_DeleteRememberedEntities(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.DeleteRememberedEntities); return true; }

    [MethodImpl(Inline)] static bool Prefix_Save(ref ProfilerTimer __local_timer)
    { __local_timer = Profiler.Start(Keys.Save); return true; }
}
