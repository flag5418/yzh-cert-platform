namespace YZH.Core.Stand.Helpers;

/// <summary>ID 生成器（雪花算法简化版）</summary>
public static class IdGenerator
{
    private static readonly DateTime Epoch = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int WorkerIdBits = 5;
    private const int DatacenterIdBits = 5;
    private const int SequenceBits = 12;
    private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);
    private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);
    private const long SequenceMask = -1L ^ (-1L << SequenceBits);
    private const int WorkerIdShift = SequenceBits;
    private const int DatacenterIdShift = SequenceBits + WorkerIdBits;
    private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

    private static long _lastTimestamp = -1L;
    private static long _sequence = 0L;
    private static readonly object Lock = new();
    private static readonly long WorkerId = 1;
    private static readonly long DatacenterId = 1;

    /// <summary>生成雪花 ID</summary>
    public static long NextId()
    {
        lock (Lock)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (timestamp < _lastTimestamp)
                throw new InvalidOperationException("时钟回拨异常");
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & SequenceMask;
                if (_sequence == 0)
                {
                    while (timestamp <= _lastTimestamp)
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }
            else
            {
                _sequence = 0;
            }
            _lastTimestamp = timestamp;
            return ((timestamp - new DateTimeOffset(Epoch).ToUnixTimeMilliseconds()) << TimestampLeftShift)
                   | (DatacenterId << DatacenterIdShift)
                   | (WorkerId << WorkerIdShift)
                   | _sequence;
        }
    }

    /// <summary>生成雪花 ID 字符串</summary>
    public static string NextIdString() => NextId().ToString();
}
