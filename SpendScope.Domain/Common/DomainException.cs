namespace SpendScope.Domain.Common;

/// <summary>Thrown when an operation would break a domain invariant. Mapped to 422 by the Api.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public DomainException()
        : this("domain_rule_violation", "A domain rule was violated.")
    {
    }

    public DomainException(string message)
        : this("domain_rule_violation", message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = "domain_rule_violation";
    }

    public string Code { get; }
}
