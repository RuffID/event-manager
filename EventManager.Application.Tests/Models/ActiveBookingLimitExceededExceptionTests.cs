using EventManager.Domain.Exceptions;
using Xunit;

namespace EventManager.Application.Tests.Models
{
    public class ActiveBookingLimitExceededExceptionTests
    {
        [Theory]
        [InlineData(10)]
        [InlineData(25)]
        public void Constructor_IncludesLimitInMessage_WhenLimitIsValid(int limit)
        {
            // Arrange
            string expectedLimit = limit.ToString();

            // Act
            ActiveBookingLimitExceededException exception = new ActiveBookingLimitExceededException(limit);

            // Assert
            Assert.Equal(limit, exception.Limit);
            Assert.Contains(expectedLimit, exception.Message);
        }
    }
}
