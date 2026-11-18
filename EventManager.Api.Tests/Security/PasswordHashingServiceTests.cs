using EventManager.Infrastructure.Security;
using Xunit;

namespace EventManager.Api.Tests.Security
{
    public class PasswordHashingServiceTests
    {
        [Fact]
        public void Hash_ReturnsVerifiableHash_WhenPasswordIsProvided()
        {
            // Arrange
            PasswordHashingService service = new PasswordHashingService();
            string password = "Учебный пароль 123";

            // Act
            string hash = service.Hash(password);

            // Assert
            Assert.NotEqual(password, hash);
            Assert.True(service.Verify(password, hash));
        }

        [Fact]
        public void Verify_ReturnsFalse_WhenPasswordDoesNotMatch()
        {
            // Arrange
            PasswordHashingService service = new PasswordHashingService();
            string hash = service.Hash("correct-password");

            // Act
            bool matches = service.Verify("incorrect-password", hash);

            // Assert
            Assert.False(matches);
        }

        [Fact]
        public void Hash_UsesDifferentSalt_WhenPasswordIsHashedTwice()
        {
            // Arrange
            PasswordHashingService service = new PasswordHashingService();
            string password = "same-password";

            // Act
            string firstHash = service.Hash(password);
            string secondHash = service.Hash(password);

            // Assert
            Assert.NotEqual(firstHash, secondHash);
            Assert.True(service.Verify(password, firstHash));
            Assert.True(service.Verify(password, secondHash));
        }
    }
}
