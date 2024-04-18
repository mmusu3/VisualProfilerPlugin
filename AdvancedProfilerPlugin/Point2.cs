using System.Numerics;

namespace AdvancedProfiler;

struct Point2
{
    public int X;
    public int Y;

    public static readonly Point2 Zero = new Point2(0, 0);
    public static readonly Point2 One = new Point2(1, 1);

    public Point2(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Point2(int value)
    {
        X = value;
        Y = value;
    }

    public Point2(Vector2 vector)
    {
        X = (int)vector.X;
        Y = (int)vector.Y;
    }

    public readonly void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    #region --- Arithmetic ---

    public readonly Point2 Add(Point2 value) => new Point2(X + value.X, Y + value.Y);
    //public readonly Point2 Add(Size2 value) => new Point2(X + value.Width, Y + value.Height);
    public readonly Point2 Subtract(Point2 value) => new Point2(X - value.X, Y - value.Y);
    //public readonly Point2 Subtract(Size2 value) => new Point2(X - value.Width, Y - value.Height);
    public readonly Point2 Multiply(Point2 value) => new Point2(X * value.X, Y * value.Y);
    public readonly Point2 Divide(Point2 value) => new Point2(X / value.X, Y / value.Y);
    public readonly Point2 Add(int value) => new Point2(X + value, Y + value);
    public readonly Point2 Subtract(int value) => new Point2(X - value, Y - value);
    public readonly Point2 Multiply(float value) => new Point2((int)(X * value), (int)(Y * value));
    public readonly Point2 Divide(float value) => new Point2((int)(X / value), (int)(Y / value));
    public readonly Point2 Negate() => new Point2(-X, -Y);

    // ================================
    //          Static Methods
    // ================================
    public static Point2 Add(Point2 value1, Point2 value2) => new Point2(value1.X + value2.X, value1.Y + value2.Y);
    //public static Point2 Add(Point2 value1, Size2 value2) => new Point2(value1.X + value2.Width, value1.Y + value2.Height);
    public static Point2 Subtract(Point2 value1, Point2 value2) => new Point2(value1.X - value2.X, value1.Y - value2.Y);
    //public static Point2 Subtract(Point2 value1, Size2 value2) => new Point2(value1.X - value2.Width, value1.Y - value2.Height);

    public static void Multiply(ref Point2 value1, ref Point2 value2, out Point2 result)
    {
        result = new Point2(value1.X * value2.X, value1.Y * value2.Y);
    }

    public static Point2 Multiply(Point2 value1, Point2 value2)
    {
        Point2 result;
        Multiply(ref value1, ref value2, out result);
        return result;
    }

    public static void Multiply(ref Point2 value1, int value2, out Point2 result)
    {
        result = new Point2(value1.X * value2, value1.Y * value2);
    }

    public static Point2 Multiply(Point2 value1, int value2)
    {
        Point2 result;
        Multiply(ref value1, value2, out result);
        return result;
    }

    public static void Divide(ref Point2 value1, ref Point2 value2, out Point2 result)
    {
        result = new Point2(value1.X / value2.X, value1.Y / value2.Y);
    }

    public static Point2 Divide(Point2 value1, Point2 value2)
    {
        Point2 result;
        Divide(ref value1, ref value2, out result);
        return result;
    }

    public static void Divide(ref Point2 value1, int value2, out Point2 result)
    {
        result = new Point2(value1.X / value2, value1.Y / value2);
    }

    public static Point2 Divide(Point2 value1, int value2)
    {
        Point2 result;
        Divide(ref value1, value2, out result);
        return result;
    }

    public static Point2 Negate(Point2 value) => new Point2(-value.X, -value.Y);

    public static void Negate(ref Point2 value, out Point2 result)
    {
        result.X = -value.X;
        result.Y = -value.Y;
    }

    public static Point2 Min(Point2 value1, Point2 value2) => new Point2(System.Math.Min(value1.X, value2.X), System.Math.Min(value1.Y, value2.Y));
    public static Point2 Max(Point2 value1, Point2 value2) => new Point2(System.Math.Max(value1.X, value2.X), System.Math.Max(value1.Y, value2.Y));

    #endregion

    #region --- Operators ---

    public static Point2 operator +(Point2 value1, Point2 value2) => Add(value1, value2);
    //public static Point2 operator +(Point2 value1, Size2 value2) => Add(value1, value2);
    public static Point2 operator -(Point2 value1, Point2 value2) => Subtract(value1, value2);
    //public static Point2 operator -(Point2 value1, Size2 value2) => Subtract(value1, value2);
    public static Point2 operator *(Point2 value1, Point2 value2) => Multiply(value1, value2);
    public static Point2 operator *(Point2 value1, int value2) => Multiply(value1, value2);
    public static Point2 operator *(int value1, Point2 value2) => Multiply(value2, value1);
    public static Point2 operator /(Point2 value1, Point2 value2) => Divide(value1, value2);
    public static Point2 operator /(Point2 value1, int value2) => Divide(value1, value2);
    public static Point2 operator -(Point2 value) => new Point2(-value.X, -value.Y);
    public static bool operator ==(Point2 value1, Point2 value2) => value1.Equals(value2);
    public static bool operator !=(Point2 value1, Point2 value2) => !value1.Equals(value2);

    #endregion

    public readonly bool Equals(Point2 other) => X == other.X && Y == other.Y;
    public override readonly bool Equals(object? obj) => obj is Point2 other && Equals(other);

    public override readonly int GetHashCode() => (X * 397) ^ Y;

    //public static implicit operator Size2(Point2 point) => new Size2(point.X, point.Y);
    //public static implicit operator Vector2I(Point2 point) => new Vector2I(point.X, point.Y);
    public static implicit operator Vector2(Point2 point) => new Vector2(point.X, point.Y);

    //public readonly Size2 ToSize2() => new Size2(X, Y);
    //public readonly Vector2I ToVector2I() => new Vector2I(X, Y);
    public readonly Vector2 ToVector2() => new Vector2(X, Y);

    public override readonly string ToString() => "X: " + X + ", Y: " + Y;
}