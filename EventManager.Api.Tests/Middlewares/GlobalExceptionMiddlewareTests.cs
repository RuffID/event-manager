using EventManager.Domain.Exceptions;
using EventManager.Api.Middlewares;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

        [Fact]
        public async Task InvokeAsync_ReturnsSafeProblemDetails_WhenUnexpectedExceptionOccurs()
        {
            // Arrange
            const string SECRET = "Database password: private-value";
            RequestDelegate next = _ => throw new InvalidOperationException(SECRET);
            GlobalExceptionMiddleware middleware = new GlobalExceptionMiddleware(
                next, NullLogger<GlobalExceptionMiddleware>.Instance);
            DefaultHttpContext context = new DefaultHttpContext();
            using MemoryStream body = new MemoryStream();
            context.Response.Body = body;

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            body.Position = 0;
            using JsonDocument json = await JsonDocument.ParseAsync(body,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Не удалось обработать запрос", json.RootElement.GetProperty("title").GetString());
            Assert.Equal("Внутренняя ошибка сервера.", json.RootElement.GetProperty("detail").GetString());
            Assert.DoesNotContain(SECRET, json.RootElement.GetRawText());
            Assert.False(json.RootElement.TryGetProperty("stackTrace", out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InvokeAsync_DoesNotOverwriteStartedResponse(bool conflict)
        {
            // Arrange
            Exception failure = new InvalidOperationException("Unexpected failure.");
            if (conflict)
            {
                failure = new NoAvailableSeatsException();
            }

            using MemoryStream body = new MemoryStream();
            body.Write([1, 2, 3]);
            StartedResponseFeature feature = new StartedResponseFeature
            {
                Body = body,
                StatusCode = StatusCodes.Status202Accepted
            };
            feature.Headers["X-Original"] = "preserved";
            DefaultHttpContext context = new DefaultHttpContext();
            context.Features.Set<IHttpResponseFeature>(feature);
            GlobalExceptionMiddleware middleware = new GlobalExceptionMiddleware(
                _ => throw failure, NullLogger<GlobalExceptionMiddleware>.Instance);

            // Act
            Func<Task> action = () => middleware.InvokeAsync(context);

            // Assert
            Assert.Same(failure, await Record.ExceptionAsync(action));
            Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
            Assert.Equal("preserved", context.Response.Headers["X-Original"]);
            Assert.Equal(new byte[] { 1, 2, 3 }, body.ToArray());
        }

        private class StartedResponseFeature : HttpResponseFeature
        {
            public override bool HasStarted => true;
        }
    }
}
