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
        public async Task InvokeAsync_PropagatesCancellation_WhenRequestIsAborted()
        {
            // Arrange
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            DefaultHttpContext context = new DefaultHttpContext { RequestAborted = cancellation.Token };
            RequestDelegate next = _ => Task.FromCanceled(cancellation.Token);
            GlobalExceptionMiddleware middleware = new GlobalExceptionMiddleware(
                next, NullLogger<GlobalExceptionMiddleware>.Instance);

            // Act
            Task action = middleware.InvokeAsync(context);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
            Assert.NotEqual(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_ReturnsConflict_WhenNoSeatsAreAvailable()
        {
            // Arrange
            RequestDelegate next = _ => throw new NoAvailableSeatsException();
            GlobalExceptionMiddleware middleware = new GlobalExceptionMiddleware(
                next,
                NullLogger<GlobalExceptionMiddleware>.Instance);
            DefaultHttpContext context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        }
    }
}
