using System;
namespace VOL.Core.Utilities
{
    public class IdWorker
    {
        private static long machineId;
        private static long datacenterId;
        private static long sequence = 0L;
        private static long twepoch = 1288834974657L;
        private static long machineIdBits = 5L;
        private static long datacenterIdBits = 5L;
        private static long maxMachineId = -1L ^ (-1L << (int)machineIdBits);
        private static long maxDatacenterId = -1L ^ (-1L << (int)datacenterIdBits);
        private static long sequenceBits = 12L;
        private static long machineIdShift = sequenceBits;
        private static long datacenterIdShift = sequenceBits + machineIdBits;
        private static long timestampLeftShift = sequenceBits + machineIdBits + datacenterIdBits;
        private static long sequenceMask = -1L ^ (-1L << (int)sequenceBits);
        private static long lastTimestamp = -1L;

        private static object lockSnowObj = new object();

        public IdWorker(long machineId = 1, long datacenterId = 1)
        {
            if (machineId > maxMachineId || machineId < 0)
            {
                throw new Exception("machineId can't be greater than maxMachineId or less than 0");
            }
            if (datacenterId > maxDatacenterId || datacenterId < 0)
            {
                throw new Exception("datacenterId can't be greater than maxDatacenterId or less than 0");
            }
            IdWorker.machineId = machineId;
            IdWorker.datacenterId = datacenterId;
        }

        public long NextId()
        {
            lock (lockSnowObj)
            {
                long timestamp = TimeGen();
                if (timestamp < lastTimestamp)
                {
                    throw new Exception("Clock moved backwards. Refusing to generate id for " + (lastTimestamp - timestamp) + " milliseconds");
                }

                if (lastTimestamp == timestamp)
                {
                    sequence = (sequence + 1) & sequenceMask;
                    if (sequence == 0)
                    {
                        // 如果在同一毫秒内生成的ID数量超过了限制，等待下一毫秒
                        timestamp = TilNextMillis(lastTimestamp);
                    }
                }
                else
                {
                    sequence = 0L;
                }

                lastTimestamp = timestamp;

                return ((timestamp - twepoch) << (int)timestampLeftShift) |
                    (datacenterId << (int)datacenterIdShift) |
                    (machineId << (int)machineIdShift) |
                    sequence;
            }
        }

        private long TilNextMillis(long lastTimestamp)
        {
            long timestamp = TimeGen();
            while (timestamp <= lastTimestamp)
            {
                timestamp = TimeGen();
            }
            return timestamp;
        }

        private long TimeGen()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
