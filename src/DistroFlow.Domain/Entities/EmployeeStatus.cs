namespace DistroFlow.Domain.Entities;

public enum EmployeeStatus
{
    /// <summary>Can sign in and act in the system.</summary>
    Active = 1,

    /// <summary>Left or suspended. Personal data is kept only until the retention period ends.</summary>
    Deactivated = 2,

    /// <summary>Personal data erased. The row remains so past orders and audit entries still resolve.</summary>
    Anonymised = 3
}
