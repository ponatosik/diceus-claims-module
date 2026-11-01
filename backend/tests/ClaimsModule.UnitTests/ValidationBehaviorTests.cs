using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.UnitTests;

public class ValidationBehaviorTests
{
    private record PlainRequest : IRequest<string>;

    private record WarningAwareRequest : IRequest<string>, IHasValidationWarnings
    {
        public List<ValidationFailure> Warnings { get; } = [];
    }

    [Fact]
    public async Task No_validators_registered_calls_next_without_throwing()
    {
        var behavior = new ValidationBehavior<PlainRequest, string>([]);
        var nextCalled = false;

        var result = await behavior.Handle(new PlainRequest(), _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task All_error_failures_throw_ValidationException_and_next_is_never_called()
    {
        var failures = new List<ValidationFailure>
        {
            new("Field1", "Field1 is wrong") { Severity = Severity.Error },
            new("Field2", "Field2 is wrong") { Severity = Severity.Error },
        };
        var behavior = new ValidationBehavior<PlainRequest, string>([new FakeValidator<PlainRequest>(failures)]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new PlainRequest(), _ =>
            {
                nextCalled = true;
                return Task.FromResult("ok");
            }, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.Equal(2, exception.Errors.Count());
    }

    [Fact]
    public async Task Mixed_error_and_warning_failures_throw_with_only_the_error_ones()
    {
        var failures = new List<ValidationFailure>
        {
            new("Field1", "Field1 is wrong") { Severity = Severity.Error },
            new("Field2", "Field2 is a warning") { Severity = Severity.Warning },
        };
        var behavior = new ValidationBehavior<PlainRequest, string>([new FakeValidator<PlainRequest>(failures)]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new PlainRequest(), _ => Task.FromResult("ok"), CancellationToken.None));

        Assert.Single(exception.Errors);
        Assert.Equal("Field1", exception.Errors.Single().PropertyName);
    }

    [Fact]
    public async Task All_warning_failures_on_a_warnings_aware_request_do_not_throw_and_attach_to_the_request()
    {
        var failures = new List<ValidationFailure>
        {
            new("Field1", "Field1 is a warning") { Severity = Severity.Warning },
        };
        var behavior = new ValidationBehavior<WarningAwareRequest, string>([new FakeValidator<WarningAwareRequest>(failures)]);
        var request = new WarningAwareRequest();
        var nextCalled = false;

        var result = await behavior.Handle(request, _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
        Assert.Single(request.Warnings);
    }

    [Fact]
    public async Task All_warning_failures_on_a_request_without_the_interface_do_not_throw_and_are_dropped()
    {
        var failures = new List<ValidationFailure>
        {
            new("Field1", "Field1 is a warning") { Severity = Severity.Warning },
        };
        var behavior = new ValidationBehavior<PlainRequest, string>([new FakeValidator<PlainRequest>(failures)]);
        var nextCalled = false;

        var result = await behavior.Handle(new PlainRequest(), _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    private class FakeValidator<T> : AbstractValidator<T>
    {
        public FakeValidator(List<ValidationFailure> failures)
        {
            RuleFor(x => x).Custom((_, context) =>
            {
                foreach (var failure in failures)
                {
                    context.AddFailure(failure);
                }
            });
        }
    }
}
