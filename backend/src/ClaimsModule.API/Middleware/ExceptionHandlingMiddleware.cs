using System.Text.Json;
using ClaimsModule.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.API.Middleware;

/// <summary>
/// Maps exception type to HTTP status code (ADR-0006). 422 is reserved for validation/business-rule
/// failures only — a <see cref="ValidationException"/> gets the field-keyed <c>errors</c> shape from
/// §10.5, a <see cref="BusinessRuleViolationException"/> gets the <c>blockingConditions</c> shape from
/// ADR-0008. Everything else maps to the status code that actually fits it.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var response = MapToErrorResponse(exception);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = response.Status;
            await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonOptions);
        }
    }

    private static ErrorResponse MapToErrorResponse(Exception exception) => exception switch
    {
        ValidationException validationException => new ErrorResponse
        {
            Type = "ValidationError",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status422UnprocessableEntity,
            Errors = validationException.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray()),
        },
        BusinessRuleViolationException businessRuleViolationException => new ErrorResponse
        {
            Type = "BusinessRuleViolation",
            Title = "One or more business rules prevent this request from completing.",
            Status = StatusCodes.Status422UnprocessableEntity,
            BlockingConditions = businessRuleViolationException.BlockingConditions,
        },
        NotFoundException notFoundException => new ErrorResponse
        {
            Type = "NotFound",
            Title = notFoundException.Message,
            Status = StatusCodes.Status404NotFound,
        },
        DbUpdateConcurrencyException => new ErrorResponse
        {
            Type = "ConcurrencyConflict",
            Title = "The resource was modified by another request. Reload and try again.",
            Status = StatusCodes.Status409Conflict,
        },
        _ => new ErrorResponse
        {
            Type = "InternalServerError",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
        },
    };
}
