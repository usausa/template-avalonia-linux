namespace Template.LinuxApp.Components.Platform;

public sealed class MetricHistory
{
    private readonly double[] buffer;

    private int start;

    private int count;

    public int Capacity => buffer.Length;

    public double Last => count > 0 ? buffer[(start + count - 1) % buffer.Length] : Double.NaN;

    public MetricHistory(int capacity)
    {
        buffer = new double[capacity];
    }

    public void Add(double value)
    {
        if (count < buffer.Length)
        {
            buffer[(start + count) % buffer.Length] = value;
            count++;
        }
        else
        {
            buffer[start] = value;
            start = (start + 1) % buffer.Length;
        }
    }

    public double[] ToArray()
    {
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = buffer[(start + i) % buffer.Length];
        }

        return values;
    }
}
