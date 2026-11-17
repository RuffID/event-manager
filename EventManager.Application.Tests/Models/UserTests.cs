using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Xunit;

namespace EventManager.Application.Tests.Models
{
    public class UserTests
    {
        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Admin)]
        public void Constructor_PreservesAccountData_WhenValuesAreValid(UserRole role)
        {
            // Arrange
            Guid id = Guid.NewGuid();
            string login = "student";
            string passwordHash = "stored-password-hash";

            // Act
            User user = new User(id, login, passwordHash, role);

            // Assert
            Assert.Equal(id, user.Id);
            Assert.Equal(login, user.Login);
            Assert.Equal(passwordHash, user.PasswordHash);
            Assert.Equal(role, user.Role);
        }

        [Fact]
        public void Constructor_UsesUserRole_WhenRoleIsNotSpecified()
        {
            // Arrange
            Guid id = Guid.NewGuid();

            // Act
            User user = new User(id, "student", "stored-password-hash");

            // Assert
            Assert.Equal(UserRole.User, user.Role);
        }

        [Fact]
        public void Constructor_ThrowsArgumentException_WhenIdIsEmpty()
        {
            // Arrange
            Guid id = Guid.Empty;

            // Act
            Action action = () => new User(id, "student", "stored-password-hash");

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("id", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_ThrowsArgumentException_WhenLoginIsBlank(string login)
        {
            // Arrange
            Guid id = Guid.NewGuid();

            // Act
            Action action = () => new User(id, login, "stored-password-hash");

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("login", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_ThrowsArgumentException_WhenPasswordHashIsBlank(string passwordHash)
        {
            // Arrange
            Guid id = Guid.NewGuid();

            // Act
            Action action = () => new User(id, "student", passwordHash);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("passwordHash", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsArgumentOutOfRangeException_WhenRoleIsUnknown()
        {
            // Arrange
            Guid id = Guid.NewGuid();
            UserRole role = (UserRole)100;

            // Act
            Action action = () => new User(id, "student", "stored-password-hash", role);

            // Assert
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(action);
            Assert.Equal("role", exception.ParamName);
        }
    }
}
