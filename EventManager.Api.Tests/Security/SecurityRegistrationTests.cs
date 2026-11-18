using EventManager.Application.Abstractions.Security;
using EventManager.Infrastructure.Extensions;
using EventManager.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventManager.Api.Tests.Security
{
    public class SecurityRegistrationTests
    {
        [Fact]
        public void AddSecurityComponents_ResolvesComponents_WhenSettingsAreValid()
        {
            // Arrange
            Dictionary<string, string?> values = CreateSettings();
            using ServiceProvider provider = CreateProvider(values);
            using IServiceScope scope = provider.CreateScope();

            // Act
            provider.GetRequiredService<IStartupValidator>().Validate();
            IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            IJwtTokenGenerator generator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
            JwtOptions options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

            // Assert
            Assert.IsType<PasswordHashingService>(hasher);
            Assert.IsType<JwtTokenGenerator>(generator);
            Assert.Equal(values["Jwt:Issuer"], options.Issuer);
            Assert.Equal(values["Jwt:Audience"], options.Audience);
            Assert.Equal(values["Jwt:SigningKeyBase64"], options.SigningKeyBase64);
            Assert.Equal(15, options.LifetimeMinutes);
        }

        [Theory]
        [InlineData("Jwt:Issuer", "")]
        [InlineData("Jwt:Audience", " ")]
        [InlineData("Jwt:SigningKeyBase64", "")]
        [InlineData("Jwt:SigningKeyBase64", "invalid-base64")]
        [InlineData("Jwt:SigningKeyBase64", "c2hvcnQ=")]
        [InlineData("Jwt:LifetimeMinutes", "0")]
        [InlineData("Jwt:LifetimeMinutes", "-1")]
        public void AddSecurityComponents_RejectsInvalidSettings_DuringStartupValidation(string key, string value)
        {
            // Arrange
            Dictionary<string, string?> values = CreateSettings();
            values[key] = value;
            using ServiceProvider provider = CreateProvider(values);
            IStartupValidator validator = provider.GetRequiredService<IStartupValidator>();

            // Act
            Action action = validator.Validate;

            // Assert
            Assert.Throws<OptionsValidationException>(action);
        }

        private static Dictionary<string, string?> CreateSettings()
        {
            return new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-api",
                ["Jwt:SigningKeyBase64"] = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
                ["Jwt:LifetimeMinutes"] = "15"
            };
        }

        private static ServiceProvider CreateProvider(Dictionary<string, string?> values)
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            ServiceCollection services = new ServiceCollection();
            services.AddSecurityComponents(configuration);

            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
        }
    }
}
