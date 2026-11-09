namespace DistroFlow.Application.Interfaces.Identity;

public enum SetLoginActiveResult
{
    Succeeded = 0,

    /// <summary>
    /// Cannot activate: the email is now used by another active account (e.g. the person
    /// joined another distributor after leaving this one).
    /// </summary>
    EmailInUse = 1
}
