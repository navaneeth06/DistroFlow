namespace DistroFlow.Domain.Entities;

/// <summary>
/// A person who works for a distributor (tenant) and uses DistroFlow.
/// </summary>
/// <remarks>
/// Privacy (DPDP Act, 2023):
/// - Data minimisation: only what is needed to identify the actor on orders, approvals and audit
///   entries. Do not add Aadhaar, PAN, date of birth, home address, bank details or photos;
///   DistroFlow does not run payroll or HR.
/// - The login email lives only on the Identity user. It is not copied here.
/// - Erasure: <see cref="Anonymise"/> removes personal data but keeps the row and its Id, so
///   records that must be retained (invoices, audit) still point to "a former employee".
/// </remarks>
public class Employee
{
    public const string AnonymisedName = "Former employee";

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Login account (Identity user). Null after anonymisation, when the account is deleted.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>Optional internal code assigned by the distributor, unique per tenant.</summary>
    public string? EmployeeCode { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Work phone in E.164 format, optional.</summary>
    public string? Phone { get; set; }

    public EmployeeStatus Status { get; private set; } = EmployeeStatus.Active;

    public DateTime? DeactivatedAtUtc { get; private set; }

    public DateTime? AnonymisedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsActive => Status == EmployeeStatus.Active;

    public void Deactivate(DateTime utcNow)
    {
        if (Status != EmployeeStatus.Active)
        {
            return;
        }

        Status = EmployeeStatus.Deactivated;
        DeactivatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Changes the profile details. Not allowed once anonymised: that data was erased on
    /// purpose and must not be re-entered.
    /// </summary>
    public void UpdateProfile(string fullName, string? phone, string? employeeCode, DateTime utcNow)
    {
        if (Status == EmployeeStatus.Anonymised)
        {
            throw new InvalidOperationException("An anonymised employee cannot be edited.");
        }

        FullName = fullName;
        Phone = phone;
        EmployeeCode = employeeCode;
        UpdatedAtUtc = utcNow;
    }

    public void Reactivate(DateTime utcNow)
    {
        if (Status == EmployeeStatus.Anonymised)
        {
            throw new InvalidOperationException("An anonymised employee cannot be reactivated.");
        }

        if (Status == EmployeeStatus.Active)
        {
            return;
        }

        Status = EmployeeStatus.Active;
        DeactivatedAtUtc = null;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Erases personal data (right to erasure, or the end of the retention period).
    /// The caller must also delete the Identity user in the same transaction.
    /// </summary>
    public void Anonymise(DateTime utcNow)
    {
        if (Status == EmployeeStatus.Anonymised)
        {
            return;
        }

        DeactivatedAtUtc ??= utcNow;
        Status = EmployeeStatus.Anonymised;
        AnonymisedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;

        FullName = AnonymisedName;
        Phone = null;
        EmployeeCode = null;
        UserId = null;
    }
}
