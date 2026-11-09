namespace DistroFlow.Application.Interfaces.Identity;

/// <summary>The parts of an employee that live on their login account.</summary>
public sealed record LoginInfo(string? Email, IReadOnlyList<string> Roles);
