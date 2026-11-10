using FCG.Users.Domain.Commons;
using FCG.Users.Domain.Commons.Result;
using FluentValidation.Results;

namespace FCG.Users.Application.Extensions;

public static class ValidationExtensions
{
    public static InvalidResult<T>? ToInvalidResult<T>(this ValidationResult validationResult)
    {
        if (validationResult.IsValid)
        {
            return null;
        }

        var errors = validationResult.Errors
            .Select(e => new Error(e.ErrorMessage, e.PropertyName));

        return InvalidResult<T>.Create(errors);
    }
}
