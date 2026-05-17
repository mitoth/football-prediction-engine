using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace WcPredictions.Bff;

public static class ClerkAuthentication
{
    // Clerk issues RS256 JWTs verified via its JWKS (Clerk:Authority).
    // Phase 0 has no real Clerk tenant yet, so a Development-only symmetric
    // signing key (Clerk:DevSigningKey) is also accepted, letting tests mint a
    // valid token and exercise the 200-with-JWT / 401-without path now. The dev
    // key path is ignored unless the environment is Development.
    public static IHostApplicationBuilder AddClerkAuth(this IHostApplicationBuilder builder)
    {
        var cfg = builder.Configuration.GetSection("Clerk");
        var authority = cfg["Authority"];
        var audience = cfg["Audience"];
        var devKey = cfg["DevSigningKey"];
        var isDev = builder.Environment.IsDevelopment();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    options.Authority = authority;          // enables Clerk JWKS discovery
                    options.RequireHttpsMetadata = !isDev;
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(authority),
                    ValidIssuer = authority,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };

                if (isDev && !string.IsNullOrWhiteSpace(devKey))
                {
                    // Accept tokens signed with the dev key in addition to Clerk's
                    // JWKS keys. JwtBearer unions configured keys with discovered ones.
                    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(devKey));
                    options.TokenValidationParameters.IssuerSigningKey = key;
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                }
            });

        builder.Services.AddAuthorization();
        return builder;
    }
}
