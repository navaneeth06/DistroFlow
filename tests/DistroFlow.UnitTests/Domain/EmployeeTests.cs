using DistroFlow.Domain.Entities;

namespace DistroFlow.UnitTests.Domain;

public class EmployeeTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);

    private static Employee CreateEmployee() => new()
    {
        TenantId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        EmployeeCode = "EMP-001",
        FullName = "Ravi Kumar",
        Phone = "+919876543210"
    };

    [Fact]
    public void New_employee_is_active()
    {
        var employee = CreateEmployee();

        Assert.Equal(EmployeeStatus.Active, employee.Status);
        Assert.True(employee.IsActive);
        Assert.Null(employee.DeactivatedAtUtc);
    }

    [Fact]
    public void Deactivate_keeps_personal_data_and_records_time()
    {
        var employee = CreateEmployee();

        employee.Deactivate(Now);

        Assert.Equal(EmployeeStatus.Deactivated, employee.Status);
        Assert.Equal(Now, employee.DeactivatedAtUtc);
        Assert.Equal("Ravi Kumar", employee.FullName);
        Assert.NotNull(employee.UserId);
    }

    [Fact]
    public void Reactivate_clears_deactivation()
    {
        var employee = CreateEmployee();
        employee.Deactivate(Now);

        employee.Reactivate(Now.AddDays(1));

        Assert.True(employee.IsActive);
        Assert.Null(employee.DeactivatedAtUtc);
    }

    [Fact]
    public void Anonymise_removes_all_personal_data_but_keeps_identity_of_row()
    {
        var employee = CreateEmployee();
        var id = employee.Id;
        var tenantId = employee.TenantId;

        employee.Anonymise(Now);

        Assert.Equal(EmployeeStatus.Anonymised, employee.Status);
        Assert.Equal(Employee.AnonymisedName, employee.FullName);
        Assert.Null(employee.Phone);
        Assert.Null(employee.EmployeeCode);
        Assert.Null(employee.UserId);
        Assert.Equal(Now, employee.AnonymisedAtUtc);
        Assert.Equal(Now, employee.DeactivatedAtUtc);
        Assert.Equal(id, employee.Id);
        Assert.Equal(tenantId, employee.TenantId);
    }

    [Fact]
    public void Anonymise_keeps_original_deactivation_time()
    {
        var employee = CreateEmployee();
        employee.Deactivate(Now);

        employee.Anonymise(Now.AddYears(1));

        Assert.Equal(Now, employee.DeactivatedAtUtc);
    }

    [Fact]
    public void UpdateProfile_changes_details_and_records_time()
    {
        var employee = CreateEmployee();

        employee.UpdateProfile("Ravi K. Kumar", null, "EMP-002", Now);

        Assert.Equal("Ravi K. Kumar", employee.FullName);
        Assert.Null(employee.Phone);
        Assert.Equal("EMP-002", employee.EmployeeCode);
        Assert.Equal(Now, employee.UpdatedAtUtc);
    }

    [Fact]
    public void Deactivated_employee_can_still_be_edited()
    {
        var employee = CreateEmployee();
        employee.Deactivate(Now);

        employee.UpdateProfile("Ravi Kumar", "+919000000000", "EMP-001", Now.AddDays(1));

        Assert.Equal("+919000000000", employee.Phone);
    }

    [Fact]
    public void Anonymised_employee_cannot_be_edited()
    {
        var employee = CreateEmployee();
        employee.Anonymise(Now);

        Assert.Throws<InvalidOperationException>(
            () => employee.UpdateProfile("Ravi Kumar", "+919876543210", "EMP-001", Now));

        Assert.Equal(Employee.AnonymisedName, employee.FullName);
        Assert.Null(employee.Phone);
    }

    [Fact]
    public void Anonymised_employee_cannot_be_reactivated()
    {
        var employee = CreateEmployee();
        employee.Anonymise(Now);

        Assert.Throws<InvalidOperationException>(() => employee.Reactivate(Now));
    }
}
