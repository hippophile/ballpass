using Microsoft.AspNetCore.Antiforgery;

namespace Ballpass.Web;

/// <summary>The one explicit antiforgery check of every plain-form POST endpoint: an invalid or missing token is a 400, never a 500.</summary>
public static class AntiforgeryCheck
{
    public static async Task<bool> IsValidAsync(this IAntiforgery antiforgery, HttpContext http)
    {
        try { await antiforgery.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
