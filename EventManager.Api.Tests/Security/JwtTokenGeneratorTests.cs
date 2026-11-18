using System.Text;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EventManager.Api.Tests.Security
{
    public class JwtTokenGeneratorTests
    {
        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Admin)]
        public async Task Generate_ReturnsValidTokenWithUserClaims_WhenUserIsProvided(UserRole role)
        {
            // Arrange
            JwtOptions settings = CreateSettings();
            JwtTokenGenerator generator = new JwtTokenGenerator(Options.Create(settings));
            User user = new User(Guid.NewGuid(), "student", "stored-password-hash", role);
            JsonWebTokenHandler handler = new JsonWebTokenHandler();
            DateTime beforeGeneration = DateTime.UtcNow;

            // Act
            string token = generator.Generate(user);
            DateTime afterGeneration = DateTime.UtcNow;
            TokenValidationResult validation = await handler.ValidateTokenAsync(token, CreateValidationParameters(settings));

            // Assert
            Assert.True(validation.IsValid);
            JsonWebToken jwt = handler.ReadJsonWebToken(token);
            Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
            Assert.Equal(user.Id.ToString("D"), jwt.Subject);
            Assert.Equal(user.Login, jwt.GetClaim(JwtRegisteredClaimNames.UniqueName).Value);
            Assert.Equal(role.ToString(), jwt.GetClaim("role").Value);
            Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
            Assert.InRange(jwt.ValidFrom, beforeGeneration.AddSeconds(-1), afterGeneration);
            Assert.Equal(TimeSpan.FromMinutes(settings.LifetimeMinutes), jwt.ValidTo - jwt.ValidFrom);
            Assert.DoesNotContain(jwt.Claims, claim => claim.Value == user.PasswordHash);
            Assert.DoesNotContain(jwt.Claims, claim => claim.Value == settings.SigningKeyBase64);
        }

        [Fact]
        public async Task Generate_UsesConfiguredSigningKey_WhenTokenIsValidatedWithAnotherKey()
        {
            // Arrange
            JwtOptions settings = CreateSettings();
            JwtTokenGenerator generator = new JwtTokenGenerator(Options.Create(settings));
            User user = new User(Guid.NewGuid(), "student", "stored-password-hash");
            string token = generator.Generate(user);
            TokenValidationParameters parameters = CreateValidationParameters(settings);
            parameters.IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("another-signing-key-for-this-test"));

            // Act
            TokenValidationResult validation = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);

            // Assert
            Assert.False(validation.IsValid);
            Assert.IsAssignableFrom<SecurityTokenException>(validation.Exception);
        }

        private static JwtOptions CreateSettings()
        {
            return new JwtOptions
            {
                Issuer = "test-issuer",
                Audience = "test-api",
                SigningKeyBase64 = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
                LifetimeMinutes = 15
            };
        }

        private static TokenValidationParameters CreateValidationParameters(JwtOptions settings)
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKeyBase64)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            };
        }
    }
}
