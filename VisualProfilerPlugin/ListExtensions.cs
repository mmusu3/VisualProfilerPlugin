using System;
using System.Collections.Generic;

namespace VisualProfiler;

static class ListExtensions
{
#if NETFRAMEWORK
    class PublicList<T>
    {
        public T[] _items = null!;
        public int _size;
        public int _version;
        public object? _syncRoot;
    }

    public static Span<T> AsSpan<T>(this List<T> list)
    {
        var pList = System.Runtime.CompilerServices.Unsafe.As<PublicList<T>>(list);
        return pList._items.AsSpan(0, pList._size);
    }
#else
    public static Span<T> AsSpan<T>(this List<T> list)
    {
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
    }
#endif
}
