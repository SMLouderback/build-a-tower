using CloudSave.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Invites;

public static class InviteCli
{
    public static bool IsMintInviteCommand(string[] args)
    {
        return args.Contains("--mint-invite", StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<int> RunMintInviteAsync(IServiceProvider services, string[] args, TextWriter output, TextWriter error)
    {
        if (!TryReadUses(args, out var uses))
        {
            await error.WriteLineAsync("Usage: --mint-invite --uses <positive integer>");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var invites = scope.ServiceProvider.GetRequiredService<InviteService>();
        var code = await invites.MintAsync(uses, expiresUtc: null, CancellationToken.None);
        await output.WriteLineAsync(code);
        return 0;
    }

    private static bool TryReadUses(string[] args, out int uses)
    {
        uses = 0;
        var index = Array.FindIndex(args, arg => string.Equals(arg, "--uses", StringComparison.OrdinalIgnoreCase));
        return index >= 0
               && index + 1 < args.Length
               && int.TryParse(args[index + 1], out uses)
               && uses > 0;
    }
}
