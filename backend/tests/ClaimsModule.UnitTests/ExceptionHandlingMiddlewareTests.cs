using System.Text.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Common.Exceptions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.UnitTests;

public class ExceptionHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(typeof(NotFoundException), 404, "NotFound")]
    [InlineData(typeof(DbUpdateConcurrencyException), 409, "ConcurrencyConflict")]
    [InlineData(typeof(InvalidOperationException), 500, "InternalServerError")]
    public async Task Maps_exception_type_to_status_and_type(Type exceptionType, int expectedStatus, string expectedType)
    {
        var exception = CreateException(exceptionType);

        var (statusCode, body) = await InvokeAsync(exception);

        Assert.Equal(expectedStatus, statusCode);
        Assert.Equal(expectedType, body.Type);
        Assert.Equal(expectedStatus, body.Status);
    }

    [Fact]
    public async Task ValidationException_maps_to_422_with_errors_grouped_by_property_name()
    {
        var exception = new FluentValidation.ValidationException([
            new ValidationFailure("LossDate", "Loss date cannot be in the future."),
            new ValidationFailure("LossDate", "Loss date is required."),
            new ValidationFailure("LossDescription", "Loss description is required and must be at least 20 characters."),
        ]);

        var (statusCode, body) = await InvokeAsync(exception);

        Assert.Equal(422, statusCode);
        Assert.Equal("ValidationError", body.Type);
        Assert.Equal("One or more validation errors occurred.", body.Title);
        Assert.NotNull(body.Errors);
        Assert.Equal(["Loss date cannot be in the future.", "Loss date is required."], body.Errors!["LossDate"]);
        Assert.Equal(["Loss description is required and must be at least 20 characters."], body.Errors!["LossDescription"]);
        Assert.Null(body.BlockingConditions);
    }

    [Fact]
    public async Task BusinessRuleViolationException_maps_to_422_with_blockingConditions()
    {
        var exception = new BusinessRuleViolationException([
            new BlockingCondition("CC-01", "No reserve components with status PendingApproval remain."),
        ]);

        var (statusCode, body) = await InvokeAsync(exception);

        Assert.Equal(422, statusCode);
        Assert.Equal("BusinessRuleViolation", body.Type);
        Assert.Null(body.Errors);
        Assert.NotNull(body.BlockingConditions);
        Assert.Single(body.BlockingConditions!);
        Assert.Equal("CC-01", body.BlockingConditions![0].Code);
    }

    [Fact]
    public async Task An_unexpected_exception_is_logged_at_error_level()
    {
        var logger = new FakeLogger();
        var middleware = new ExceptionHandlingMiddleware(logger);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("boom"));

        Assert.Contains(logger.LoggedLevels, level => level == LogLevel.Error);
    }

    [Fact]
    public async Task A_handled_business_rule_failure_is_not_logged_as_an_error()
    {
        var logger = new FakeLogger();
        var middleware = new ExceptionHandlingMiddleware(logger);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await middleware.InvokeAsync(context, _ => throw new NotFoundException("Claim", Guid.NewGuid()));

        Assert.DoesNotContain(logger.LoggedLevels, level => level == LogLevel.Error);
    }

    private static Exception CreateException(Type exceptionType)
    {
        if (exceptionType == typeof(NotFoundException))
        {
            return new NotFoundException("Claim", Guid.NewGuid());
        }

        if (exceptionType == typeof(DbUpdateConcurrencyException))
        {
            return new DbUpdateConcurrencyException();
        }

        return (Exception)Activator.CreateInstance(exceptionType)!;
    }

    private static async Task<(int StatusCode, ErrorResponse Body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware();
        await middleware.InvokeAsync(context, _ => throw exception);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonSerializer.DeserializeAsync<ErrorResponse>(context.Response.Body, ResponseJsonOptions);

        return (context.Response.StatusCode, body!);
    }

    private class FakeLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public List<LogLevel> LoggedLevels { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            LoggedLevels.Add(logLevel);

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
