using System.Text;
using CloudSave.Api.Auth;
using CloudSave.Api.Data;
using CloudSave.Api.Email;
using CloudSave.Api.Invites;
using CloudSave.Api.Saves;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CloudSavePostgres")
    ?? "Host=localhost;Port=5432;Database=cloudsave;Username=cloudsave;Password=cloudsave_dev_only";

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<CloudUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, configuredJwt) =>
    {
        var jwtOptions = configuredJwt.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<InviteService>();
builder.Services.AddScoped<EmailTokenService>();
builder.Services.AddConfiguredEmailSender(builder.Configuration);

var app = builder.Build();
if (InviteCli.IsMintInviteCommand(args))
    return await InviteCli.RunMintInviteAsync(app.Services, args, Console.Out, Console.Error);

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapSaveEndpoints();
await app.RunAsync();
return 0;

public partial class Program;
