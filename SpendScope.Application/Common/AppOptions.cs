using System.ComponentModel.DataAnnotations;

namespace SpendScope.Application.Common;

/// <summary>Bound from the "App" configuration section.</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Absolute origin used in links sent by email (password reset). Taken from configuration,
    /// never from the request's Host header, which an attacker can forge.
    /// </summary>
    [Required]
    [Url]
    public string PublicBaseUrl { get; set; } = "https://localhost:7000";
}
