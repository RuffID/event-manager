using EventManager.Application.Abstractions.Security;
using EventManager.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventManager.Infrastructure.Extensions
{
    /// <summary>Подключает компоненты безопасности и проверку их конфигурации.</summary>
    public static class SecurityServiceCollectionExtensions
    {
        /// <summary>Регистрирует хеширование паролей, генерацию JWT и проверку настроек при старте.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <param name="configuration">Конфигурация с разделом JWT.</param>
        /// <returns>Коллекция сервисов с компонентами безопасности.</returns>
        public static IServiceCollection AddSecurityComponents(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetRequiredSection(JwtOptions.SECTION_NAME))
                .Validate(IsValidJwtOptions, "JWT settings are invalid.")
                .ValidateOnStart();
            services.AddScoped<IPasswordHasher, PasswordHashingService>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

            return services;
        }

        private static bool IsValidJwtOptions(JwtOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.SigningKeyBase64) || options.LifetimeMinutes <= 0)
            {
                return false;
            }

            byte[] buffer = new byte[options.SigningKeyBase64.Length];
            bool decoded = Convert.TryFromBase64String(options.SigningKeyBase64, buffer, out int bytesWritten);

            return decoded && bytesWritten >= 32;
        }
    }
}
