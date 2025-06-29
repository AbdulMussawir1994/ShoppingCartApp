namespace ProductsApi.Helpers;

public class SnowflakeIdGenerator
{
    private static readonly object _lock = new();
    private static long _lastTimestamp = -1L;
    private static long _sequence = 0L;

    private const long Twepoch = 1288834974657L; // Custom epoch (Twitter's default)
    private const int MachineIdBits = 10;
    private const int SequenceBits = 12;

    private const long MaxMachineId = -1L ^ (-1L << MachineIdBits); // 1023
    private const long MaxSequence = -1L ^ (-1L << SequenceBits);   // 4095

    private readonly long _machineId;

    public SnowflakeIdGenerator(long machineId)
    {
        if (machineId < 0 || machineId > MaxMachineId)
            throw new ArgumentException($"Machine ID must be between 0 and {MaxMachineId}");

        _machineId = machineId;
    }

    public long GenerateId()
    {
        lock (_lock)
        {
            long timestamp = GetCurrentTimestamp();

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                {
                    // Sequence overflow, wait for next millisecond
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0L;
            }

            _lastTimestamp = timestamp;

            long id = ((timestamp - Twepoch) << (MachineIdBits + SequenceBits))
                    | (_machineId << SequenceBits)
                    | _sequence;

            return id;
        }
    }

    private long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private long WaitNextMillis(long lastTimestamp)
    {
        long timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }
}
