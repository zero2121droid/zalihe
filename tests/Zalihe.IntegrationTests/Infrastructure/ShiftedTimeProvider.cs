namespace Zalihe.IntegrationTests.Infrastructure;

/// <summary>The system clock moved ahead by <see cref="Offset"/>.</summary>
public class ShiftedTimeProvider : TimeProvider
{
    public TimeSpan Offset { get; set; }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
}
