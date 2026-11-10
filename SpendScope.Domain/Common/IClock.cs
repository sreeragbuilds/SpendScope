namespace SpendScope.Domain.Common;

/// <summary>Injected everywhere instead of <c>DateTime.UtcNow</c> so tests are deterministic.</summary>
public interface IClock
{
    DateTime UtcNow { get; }

    /// <summary>Today's calendar date in UTC.</summary>
    DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
