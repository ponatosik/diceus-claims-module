using System.Text.Json;
using ClaimsModule.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.API.Middleware;

/// <summary>
/// Maps exception type to HTTP status code (ADR-0006). 422 is reserved for validation/business-rule
/// failures only — a <see cref="ValidationException"/> gets the field-keyed <c>errors</c> shape from
/// §10.5, a <see cref="BusinessRuleViolationException"/> gets the <c>blockingConditions</c> shape from
/// ADR-0008. Everything else maps to the status code that actually fits it.
/// </summary>
public class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware>? logger = null)
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

            // Only the catch-all 500 case is actually unexpected — every other mapped exception is
            // an anticipated, already-handled business/validation outcome and would just be noise.
            if (response.Status == StatusCodes.Status500InternalServerError)
            {
                logger?.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

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
        DbUpdateException dbUpdateException when IsUniqueConstraintViolation(dbUpdateException) => new ErrorResponse
        {
            Type = "ConcurrencyConflict",
            Title = "This request conflicts with another concurrent request. Reload and try again.",
            Status = StatusCodes.Status409Conflict,
        },
        _ => new ErrorResponse
        {
            Type = "InternalServerError",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
        },
    };

    // SQL Server error 2601 ("Cannot insert duplicate key row ... with unique index") and 2627
    // ("Violation of UNIQUE KEY constraint") are the two forms a unique-index collision takes.
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
