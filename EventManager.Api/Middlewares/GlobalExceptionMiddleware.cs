using EventManager.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Middlewares
{
    /// <summary>
    /// Перехватывает необработанные исключения и формирует единообразный ответ об ошибке.
    /// </summary>
    public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        /// <summary>
        /// Выполняет следующий компонент конвейера и обрабатывает необработанные исключения.
        /// </summary>
        /// <param name="context">Контекст HTTP-запроса.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (NoAvailableSeatsException exception)
            {
                logger.LogWarning(
                    exception,
                    "Конфликт бронирования {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                if (context.Response.HasStarted)
                    throw;

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "Booking conflict",
                    exception.Message);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Необработанная ошибка {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                if (context.Response.HasStarted)
                    throw;

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "Не удалось обработать запрос",
                    "Внутренняя ошибка сервера.");
            }
        }

        private static Task WriteProblemDetailsAsync(
            HttpContext context,
            int statusCode,
            string title,
            string detail)
        {
            context.Response.Clear();
            context.Response.StatusCode = statusCode;

            return context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            });
        }
    }
}
