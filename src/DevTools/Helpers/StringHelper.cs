namespace DevTools.Helpers;

public static class StringHelper
{
    public static string? FormatIfNotNull(string? pattern, string value)
        => pattern is not null ? string.Format(pattern, value) : null;
}