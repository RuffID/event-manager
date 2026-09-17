using EventManager.Api.Models.Results;
using Xunit;

namespace EventManager.Api.Tests.Models
{
    public class ServiceResultTests
    {
        [Fact]
        public void Succeed_ThrowsArgumentNullException_WhenDataIsNull()
        {
            // Arrange

            // Act
            Action action = () => ServiceResult<string>.Succeed(null!);

            // Assert
            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void Constructor_ThrowsArgumentException_WhenErrorMessageIsEmpty()
        {
            // Arrange

            // Act
            Action action = () => new ServiceError(ServiceErrorType.Internal, " ");

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("message", exception.ParamName);
        }
    }
}
