using System.ComponentModel.DataAnnotations;
using EventManager.Application.Models.Dtos;
using Xunit;

namespace EventManager.Application.Tests.Models
{
    public class RegisterUserDtoTests
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("p", false)]
        [InlineData("1234567", false)]
        [InlineData("12345678", true)]
        [InlineData("longer-password", true)]
        public void Validate_RequiresAtLeastEightPasswordCharacters(string password, bool expectedValid)
        {
            // Arrange
            RegisterUserDto dto = new RegisterUserDto { Login = "student", Password = password };
            List<ValidationResult> errors = new List<ValidationResult>();

            // Act
            bool valid = Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true);

            // Assert
            Assert.Equal(expectedValid, valid);
            if (!expectedValid)
            {
                Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RegisterUserDto.Password)));
            }
        }
    }
}
