using System.Text.RegularExpressions;

namespace UserManagementAPI.Features.Users.Validation;

// Input validation rules
public static partial class UserValidation
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 255;
    public const int MaxPageSize = 100;

    // requireAll: true for POST/PUT (null is an error)
    // requireAll: false for PATCH (null means "not sent, skip")
    public static Dictionary<string, string> Validate(string? name, string? email, bool requireAll)
    {
        var errors = new Dictionary<string, string>();

        if (requireAll || name is not null)
        {
            var nameError = ValidateName(name);

            if (nameError is not null)
            {
                errors["name"] = nameError;
            }
        }

        if (requireAll || email is not null)
        {
            var emailError = ValidateEmail(email);

            if (emailError is not null)
            {
                errors["email"] = emailError;
            }
        }

        return errors;
    }

    // Returns an error message, or null if the value is valid
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return $"Name must be {NameMaxLength} characters or fewer.";
        }

        return null;
    }

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email is required.";
        }

        var trimmed = email.Trim();

        if (trimmed.Length > EmailMaxLength)
        {
            return $"Email must be {EmailMaxLength} characters or fewer.";
        }

        if (!EmailRegex().IsMatch(trimmed))
        {
            return "Email format is not valid.";
        }

        return null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}