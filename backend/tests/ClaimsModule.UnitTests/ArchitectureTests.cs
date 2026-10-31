using ClaimsModule.Application;
using ClaimsModule.Domain;
using NetArchTest.Rules;

namespace ClaimsModule.UnitTests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_does_not_depend_on_infrastructure_concerns()
    {
        var result = Types.InAssembly(typeof(DomainAssemblyReference).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Hangfire", "Azure", "MediatR", "FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Application_does_not_depend_on_outer_layers()
    {
        var result = Types.InAssembly(typeof(ApplicationAssemblyReference).Assembly)
            .Should()
            .NotHaveDependencyOnAny("ClaimsModule.Persistence", "ClaimsModule.Infrastructure", "ClaimsModule.API")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Violating types: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []);
}
