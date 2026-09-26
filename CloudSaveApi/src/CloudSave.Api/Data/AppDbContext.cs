using CloudSave.Api.Auth;
using CloudSave.Api.Email;
using CloudSave.Api.Invites;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CloudSave.Api.Data;

public sealed class AppDbContext : IdentityDbContext<CloudUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<EmailToken> EmailTokens => Set<EmailToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<InviteCode>(entity =>
        {
            entity.HasIndex(code => code.CodeHash).IsUnique();
            entity.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(code => code.RemainingUses).IsRequired();
            entity.Property(code => code.CreatedUtc).IsRequired();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.CloudUserId, token.FamilyId });
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(64);

            entity.HasOne(token => token.CloudUser)
                .WithMany()
                .HasForeignKey(token => token.CloudUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmailToken>(entity =>
        {
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => new { token.CloudUserId, token.Purpose });
            entity.Property(token => token.Purpose).HasMaxLength(32).IsRequired();
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();

            entity.HasOne(token => token.CloudUser)
                .WithMany()
                .HasForeignKey(token => token.CloudUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
