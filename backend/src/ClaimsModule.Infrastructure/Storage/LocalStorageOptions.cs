namespace ClaimsModule.Infrastructure.Storage;

/// <summary>Configuration for <see cref="LocalFileSystemStorageService"/>, bound from the <c>LocalStorage</c> section.</summary>
public class LocalStorageOptions
{
    public string RootPath { get; set; } = "uploads";
}
