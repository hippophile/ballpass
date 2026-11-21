namespace Ballpass.Web;

/// Open-redirect guard: local absolute paths only (no scheme-relative, backslash or control-character tricks).
public static class LocalRedirect
{
    public static bool IsSafe(string? url)
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/') return false;
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\')) return false;
        foreach (var c in url)
            if (char.IsControl(c) || c == '\\') return false;
        return true;
    }
}
