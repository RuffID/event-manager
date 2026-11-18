using EventManager.Application.Abstractions.Security;
using EventManager.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventManager.Infrastructure.Security
{
    /// <summary>Выпускает JWT с идентификатором, логином и ролью пользователя.</summary>
    /// <param name="options">Проверенные настройки выпуска токенов.</param>
    public class JwtTokenGenerator(IOptions<JwtOptions> options) : IJwtTokenGenerator
    {
        /// <inheritdoc />
        public string Generate(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            JwtOptions settings = options.Value;
            DateTime now = DateTime.UtcNow;
            byte[] keyBytes = Convert.FromBase64String(settings.SigningKeyBase64);
            SymmetricSecurityKey key = new SymmetricSecurityKey(keyBytes);
            Dictionary<string, object> claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString("D"),
                [JwtRegisteredClaimNames.UniqueName] = user.Login,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                ["role"] = user.Role.ToString()
            };
            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
            {
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                Claims = claims,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(settings.LifetimeMinutes),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
