using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Validation;

public static class ValidationProblemFactory
{
    public static ValidationProblemDetails From(ValidationResult result) => new(result.Errors
        .GroupBy(error => error.PropertyName)
        .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()));
}
