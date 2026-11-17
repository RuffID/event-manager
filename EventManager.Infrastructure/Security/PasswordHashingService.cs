using EventManager.Application.Abstractions.Security;
using Microsoft.AspNetCore.Identity;

namespace EventManager.Infrastructure.Security
{
    /// <summary>Хеширует и проверяет пароли готовым компонентом ASP.NET Core Identity.</summary>
    public class PasswordHashingService : IPasswordHasher
    {
        private readonly PasswordHasher<object> _hasher = new();
        private readonly object _accountContext = new();

        /// <inheritdoc />
        public string Hash(string password)
        {
            ArgumentNullException.ThrowIfNull(password);

            return _hasher.HashPassword(_accountContext, password);
        }

        /// <inheritdoc />
        public bool Verify(string password, string passwordHash)
        {
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(passwordHash);

            PasswordVerificationResult result = _hasher.VerifyHashedPassword(
                _accountContext, passwordHash, password);

            return result != PasswordVerificationResult.Failed;
        }
    }
}
