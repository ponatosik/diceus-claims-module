namespace ClaimsModule.Application.Claims;

/// <summary>
/// Strips path-traversal/directory components and unsafe characters from an uploaded filename before
/// it's used to build a storage path (BR-D-01). Called exactly once, by the upload command handler —
/// neither <c>IStorageService</c> implementation re-sanitises.
/// </summary>
public static class DocumentFileNameSanitizer
{
    private const string Fallback = "file";

    public static string Sanitize(string fileName)
    {
        var lastSegment = fileName.Split('/', '\\')[^1];

        var chars = lastSegment.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        var safe = new string([.. chars]).Trim('_', '.');

        return safe.Length == 0 ? Fallback : safe;
    }
}
