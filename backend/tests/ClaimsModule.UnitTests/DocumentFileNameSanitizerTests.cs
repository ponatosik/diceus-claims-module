using ClaimsModule.Application.Claims;

namespace ClaimsModule.UnitTests;

public class DocumentFileNameSanitizerTests
{
    [Fact]
    public void Sanitize_strips_path_traversal_and_directory_components()
    {
        Assert.Equal("passwd", DocumentFileNameSanitizer.Sanitize("../../etc/passwd"));
        Assert.Equal("x.txt", DocumentFileNameSanitizer.Sanitize("..\\..\\windows\\x.txt"));
    }

    [Fact]
    public void Sanitize_replaces_unsafe_characters_but_keeps_a_normal_name_unchanged()
    {
        Assert.Equal("report__final_.pdf", DocumentFileNameSanitizer.Sanitize("report (final).pdf"));
        Assert.Equal("invoice-2026.pdf", DocumentFileNameSanitizer.Sanitize("invoice-2026.pdf"));
    }

    [Fact]
    public void Sanitize_never_returns_empty()
    {
        Assert.Equal("file", DocumentFileNameSanitizer.Sanitize("../../"));
        Assert.Equal("file", DocumentFileNameSanitizer.Sanitize(".."));
    }
}
