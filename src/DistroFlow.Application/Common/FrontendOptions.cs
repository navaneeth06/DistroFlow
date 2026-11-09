using System.ComponentModel.DataAnnotations;

namespace DistroFlow.Application.Common;

/// <summary>
/// Where the React app runs, so emails can link to its pages (e.g. set-password).
/// Bound from the "Frontend" configuration section.
/// </summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;
}
