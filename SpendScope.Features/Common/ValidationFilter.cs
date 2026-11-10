using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SpendScope.Contracts.Common;

namespace SpendScope.Features.Common;

/// <summary>
/// Runs the FluentValidation validator for <typeparamref name="T"/> before the handler (spec 10.5
/// step 4). Failures return 400 ValidationProblemDetails keyed by camelCase property name, plus an
/// <c>errorCodes</c> extension with the machine-readable code per field.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (validator is null || argument is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var byField = result.Errors.GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName));
        var messages = byField.ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        var codes = byField.ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).Distinct().ToArray());

        return Results.ValidationProblem(
            messages,
            title: "One or more validation errors occurred.",
            type: "https://spendscope.dev/errors/validation",
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = ErrorCodes.ValidationFailed,
                ["errorCodes"] = codes,
            });
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
