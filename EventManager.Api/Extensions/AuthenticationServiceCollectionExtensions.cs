using EventManager.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EventManager.Api.Extensions
{
    /// <summary>Подключает проверку JWT и авторизацию запросов API.</summary>
    public static class AuthenticationServiceCollectionExtensions
    {
        /// <summary>Регистрирует Bearer-аутентификацию с общими настройками выпуска и проверки JWT.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с аутентификацией и авторизацией.</returns>
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services.AddAuthorization();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, configured) =>
                {
                    JwtOptions jwt = configured.Value;
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        RequireSignedTokens = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKeyBase64)),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        NameClaimType = "sub",
                        RoleClaimType = "role"
                    };
                    bearer.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            string? subject = context.Principal?.FindFirst("sub")?.Value;
                            if (!Guid.TryParse(subject, out Guid userId) || userId == Guid.Empty)
                            {
                                context.Fail("Invalid token subject.");
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }
    }
}
