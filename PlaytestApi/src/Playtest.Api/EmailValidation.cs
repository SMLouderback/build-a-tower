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
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
