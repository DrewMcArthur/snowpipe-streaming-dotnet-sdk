using System;

namespace SnowpipeStreaming.Util;

/// <summary>
/// Helpers for Base64URL encoding/decoding used in JWT processing.
/// </summary>
public static class Base64Url
{
    /// <summary>
    /// Encodes bytes using base64url (RFC 7515) without padding.
    /// </summary>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Decodes a base64url string (RFC 7515), accepting missing padding.
    /// </summary>
    public static byte[] Decode(string s)
    {
        string padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
