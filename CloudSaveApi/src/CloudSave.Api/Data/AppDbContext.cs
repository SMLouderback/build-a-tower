using CloudSave.Api.Auth;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
