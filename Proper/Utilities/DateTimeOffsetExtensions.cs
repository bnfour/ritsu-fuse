using Mono.Unix.Native;

namespace Bnfour.RitsuFuse.Proper.Utilities;

public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Converts a dotnet-native DateTimeOffset to a Timespec expected by mono.
    /// </summary>
    public static Timespec ToTimeSpec(this DateTimeOffset dto)
    {
        return new()
        {
            tv_sec = dto.ToUnixTimeSeconds(),
            tv_nsec = dto.Millisecond * 1_000_000
                + dto.Microsecond * 1_000
                + dto.Nanosecond
        };
    }
}
