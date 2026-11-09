using System.ComponentModel.DataAnnotations;
using DistroFlow.Application.Validators;
using DistroFlow.Domain.Constants;

namespace DistroFlow.UnitTests.Validators;

public class ValidEmployeeRolesAttributeTests
{
    private sealed class Request
    {
        [ValidEmployeeRoles]
        public List<string> Roles { get; set; } = [];
    }

    [Fact]
    public void Known_distinct_roles_are_valid()
    {
        Assert.Empty(Validate([EmployeeRoles.SalesStaff, EmployeeRoles.Approver]));
    }

    [Fact]
    public void No_roles_is_invalid()
    {
        Assert.Contains("At least one role", Assert.Single(Validate([])).ErrorMessage);
    }

    [Fact]
    public void Unknown_role_is_invalid_and_named_in_the_message()
    {
        Assert.Contains("Super Admin", Assert.Single(Validate(["Super Admin"])).ErrorMessage);
    }

    [Fact]
    public void Duplicate_roles_are_invalid()
    {
        Assert.Contains("duplicates", Assert.Single(Validate([EmployeeRoles.SalesStaff, EmployeeRoles.SalesStaff])).ErrorMessage);
    }

    [Fact]
    public void Role_names_are_case_sensitive()
    {
        // Roles must match the seeded names exactly, e.g. "Sales Staff", not "sales staff".
        Assert.Single(Validate(["sales staff"]));
    }

    private static List<ValidationResult> Validate(List<string> roles)
    {
        var request = new Request { Roles = roles };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
