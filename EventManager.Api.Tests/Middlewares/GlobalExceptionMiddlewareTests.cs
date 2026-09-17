using EventManager.Api.Exceptions;
using EventManager.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventManager.Api.Tests.Middlewares
{
    public class GlobalExceptionMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_ReturnsConflict_WhenNoSeatsAreAvailable()
        {
            RequestDelegate next = _ => throw new NoAvailableSeatsException();
            GlobalExceptionMiddleware middleware = new GlobalExceptionMiddleware(
                next,
                NullLogger<GlobalExceptionMiddleware>.Instance);
            DefaultHttpContext context = new DefaultHttpContext();

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        }
    }
}
