using SpendScope.Domain.Common;

namespace SpendScope.Infrastructure.Services;

internal sealed class SystemClock(TimeProvider time) : IClock
{
    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;
}
