using Claims.Application.Common.Exceptions;

namespace Claims.Application.Claims.Workflow;

/// <summary>Strong ETags of the form "&lt;base64 rowversion&gt;".</summary>
public static class ETags
{
    public static string Format(byte[] rowVersion) => $"\"{Convert.ToBase64String(rowVersion)}\"";

    /// <summary>
    /// Missing → 428. Returns null for anything that can't match (unquoted, weak W/"…", "*", bad base64),
    /// which the workflow reports as 412.
    /// </summary>
    public static byte[]? ParseIfMatch(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new PreconditionRequiredException();
        }

        var value = ifMatch.Trim();
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"')
        {
            return null;
        }

        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value[1..^1], buffer, out var written) ? buffer[..written] : null;
    }
}
