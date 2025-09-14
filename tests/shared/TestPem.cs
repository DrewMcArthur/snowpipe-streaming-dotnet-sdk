using System;

internal static class TestPem
{
    public static string ToPem(string type, byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"-----BEGIN {type}-----");
        for (int i = 0; i < b64.Length; i += 64)
        {
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        }
        sb.AppendLine($"-----END {type}-----");
        return sb.ToString();
    }

    
}
