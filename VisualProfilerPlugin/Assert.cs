using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace VisualProfiler;

public static class Assert
{
    public class AssertionException : Exception
    {
        public AssertionException() { }

        public AssertionException(string message)
            : base(message) { }

        public AssertionException(string message, Exception inner)
            : base(message, inner) { }
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void False([DoesNotReturnIf(true)] bool condition, string message)
    {
        if (condition) throw new AssertionException(message);
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void False([DoesNotReturnIf(true)] bool condition)
    {
        if (condition) throw new AssertionException();
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void True([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new AssertionException(message);
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void True([DoesNotReturnIf(false)] bool condition)
    {
        if (!condition) throw new AssertionException();
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void NotNull<T>([NotNull] T? obj, string message)
        where T : class
    {
        if (obj == null) throw new AssertionException(message);
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void NotNull<T>([NotNull] T? obj)
        where T : class
    {
        if (obj == null) throw new AssertionException("Object was null.");
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void Null<T>(T? obj, string message)
        where T : class
    {
        if (obj != null) throw new AssertionException(message);
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    public static void Null<T>(T? obj)
        where T : class
    {
        if (obj != null) throw new AssertionException("Object was not null.");
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    [DoesNotReturn]
    public static void Fail(string message)
    {
        throw new AssertionException(message);
    }

    [DebuggerHidden]
    [Conditional("DEBUG")]
    [DoesNotReturn]
    public static void Fail()
    {
        throw new AssertionException();
    }
}