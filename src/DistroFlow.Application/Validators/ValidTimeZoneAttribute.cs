using System.ComponentModel.DataAnnotations;

namespace DistroFlow.Application.Validators;

public class ValidTimeZoneAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string timeZoneId)
            return false;

        return TimeZoneValidator.IsValid(timeZoneId);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a valid system timezone ID.";
    }
}