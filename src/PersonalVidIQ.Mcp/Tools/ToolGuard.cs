using ModelContextProtocol;
using PersonalVidIQ.Application.YouTube;

namespace PersonalVidIQ.Mcp.Tools;

/// <summary>Turns known failures into MCP tool errors whose message Claude can read and act on.</summary>
internal static class ToolGuard
{
    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (YouTubeApiException ex)
        {
            throw new McpException($"{Label(ex.Kind)}: {ex.Message}", ex);
        }
    }

    public static TEnum ParseEnum<TEnum>(string? value, TEnum fallback, string parameter)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalised = value.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<TEnum>(normalised, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException($"Invalid {parameter} '{value}'. Use one of: {string.Join(", ", Enum.GetNames<TEnum>().Select(n => n.ToLowerInvariant()))}.");
    }

    private static string Label(YouTubeErrorKind kind) => kind switch
    {
        YouTubeErrorKind.ApiKeyMissing => "Setup needed",
        YouTubeErrorKind.ApiKeyInvalid => "API key problem",
        YouTubeErrorKind.QuotaExceeded => "Quota used up",
        YouTubeErrorKind.SearchCapReached => "Search cap reached",
        YouTubeErrorKind.NotFound => "Not found",
        YouTubeErrorKind.InvalidInput => "Invalid input",
        YouTubeErrorKind.Transient => "YouTube temporarily unavailable",
        _ => "YouTube error",
    };
}
