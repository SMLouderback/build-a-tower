using System.Net.Mail;

namespace Playtest.Api;

public static class EmailValidation
{
    public static bool TryNormalize(string? raw, out string email)
    {
        email = (raw ?? "").Trim();
        if (email.Length is 0 or > 254)
            return false;
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@'))
            return false;
        if (email.IndexOf('.', at) <= at + 1)
            return false;
        try
        {
            _ = new MailAddress(email);
            email = email.ToLowerInvariant();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Collapse Gmail/Googlemail dot/+ aliases so spam variants map to one mailbox.
    /// </summary>
    public static string Canonicalize(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@');
        if (at <= 0)
            return normalizedEmail;

        var local = normalizedEmail[..at];
        var domain = normalizedEmail[(at + 1)..];
        if (domain is "gmail.com" or "googlemail.com")
        {
            var plus = local.IndexOf('+');
            if (plus >= 0)
                local = local[..plus];
            local = local.Replace(".", "", StringComparison.Ordinal);
            domain = "gmail.com";
        }

        return local + "@" + domain;
    }

    public static bool TryNormalizeAndCanonicalize(string? raw, out string email, out string canonical)
    {
        canonical = "";
        if (!TryNormalize(raw, out email))
            return false;
        canonical = Canonicalize(email);
        return true;
    }
}
