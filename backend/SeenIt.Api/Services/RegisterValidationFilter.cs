using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using SeenIt.Api.DTOs;

namespace SeenIt.Api.Services;

public sealed class RegisterValidationFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.GetArgument<RegisterRequest>(0);
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || request.Name.Contains('\0'))
            errors["name"] = ["Informe um nome com até 120 caracteres, sem o caractere NUL."];
        if (!IsValidEmail(request.Email))
            errors["email"] = ["Informe um e-mail válido com até 200 caracteres, sem nome de exibição."];
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 8 or > 128)
            errors["password"] = ["Informe uma senha de 8 a 128 caracteres que não seja composta apenas por espaços."];
        if (errors.Count > 0)
            return ValueTask.FromResult<object?>(Results.ValidationProblem(errors));
        // Normalize identifiers only; preserve the exact password supplied.
        context.Arguments[0] = request with
        {
            Name = request.Name!.Trim(),
            Email = request.Email!.Trim().ToLowerInvariant()
        };
        return next(context);
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200) return false;
        var email = value.Trim();
        // MailAddress alone accepts display names, comments and quoted forms.
        if (email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || "<>(),;:\"\\[]".Contains(c)) ||
            !new EmailAddressAttribute().IsValid(email) || !MailAddress.TryCreate(email, out var parsed) ||
            parsed.Address != email || parsed.DisplayName.Length != 0)
            return false;
        var local = parsed.User;
        var domain = parsed.Host;
        return local.Length is > 0 and <= 64 && !local.StartsWith('.') && !local.EndsWith('.') &&
            !local.Contains("..", StringComparison.Ordinal) && domain.Contains('.') &&
            domain.Split('.').All(label => label.Length is > 0 and <= 63 &&
                !label.StartsWith('-') && !label.EndsWith('-') &&
                label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }
}
