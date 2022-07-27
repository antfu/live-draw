struct VectorX
{
    public readonly double X;
    public readonly double Y;

    public VectorX(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static VectorX operator -(VectorX a, VectorX b) => new(b.X - a.X, b.Y - a.Y);
    public static VectorX operator +(VectorX a, VectorX b) => new(a.X + b.X, a.Y + b.Y);
    public static VectorX operator *(VectorX a, double d) => new(a.X * d, a.Y * d);
    public System.Windows.Point ToPoint() => new (X, Y);
    public override string ToString() => string.Format("[{0}, {1}]", X, Y);
}
