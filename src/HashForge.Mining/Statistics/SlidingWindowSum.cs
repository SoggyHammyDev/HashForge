namespace HashForge.Mining.Statistics;

/// <summary>Sums values over a trailing period using a fixed ring of time slots. Not thread-safe.</summary>
public sealed class SlidingWindowSum
{
    private const int SlotCount = 60;

    private readonly long _slotTicks;
    private readonly long[] _slotIndexes = new long[SlotCount];
    private readonly double[] _slotSums = new double[SlotCount];

    public SlidingWindowSum(TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        _slotTicks = Math.Max(window.Ticks / SlotCount, 1);
        Array.Fill(_slotIndexes, long.MinValue);
    }

    public void Add(double value, DateTimeOffset timestamp)
    {
        var index = timestamp.UtcTicks / _slotTicks;
        var slot = (int)(index % SlotCount);
        if (_slotIndexes[slot] != index)
        {
            _slotIndexes[slot] = index;
            _slotSums[slot] = 0;
        }

        _slotSums[slot] += value;
    }

    public double Sum(DateTimeOffset now)
    {
        var newest = now.UtcTicks / _slotTicks;
        var oldest = newest - SlotCount + 1;
        var total = 0d;
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (_slotIndexes[slot] >= oldest && _slotIndexes[slot] <= newest)
            {
                total += _slotSums[slot];
            }
        }

        return total;
    }
}
