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
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Необработанная ошибка {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                if (context.Response.HasStarted)
                    throw;

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Не удалось обработать запрос",
                    Detail = "Внутренняя ошибка сервера."
                });
            }
        }
    }
}
