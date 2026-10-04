using Remittance.Core.Abstractions;

namespace Remittance.Core.Tests;

internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}
