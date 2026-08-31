using EventManager.Api.Models.Results;
using Xunit;

namespace EventManager.Api.Tests.Models
{
    public class ServiceResultTests
    {
        [Fact]
        public void Succeed_ThrowsArgumentNullException_WhenDataIsNull()
        {
            Action action = () => ServiceResult<string>.Succeed(null!);

            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void Constructor_ThrowsArgumentException_WhenErrorMessageIsEmpty()
        {
            Action action = () => new ServiceError(ServiceErrorType.Internal, " ");

            ArgumentException exception = Assert.Throws<ArgumentException>(action);
            Assert.Equal("message", exception.ParamName);
        }
    }
}
