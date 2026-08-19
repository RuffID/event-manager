using EventManager.Api.Models.Results;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Extensions
{
    /// <summary>
    /// Предоставляет методы преобразования результатов сервисного слоя в результаты действий контроллера.
    /// </summary>
    internal static class ControllerProblemExtensions
    {
        /// <summary>
        /// Преобразует результат сервиса с данными в успешный HTTP-ответ или описание ошибки.
        /// </summary>
        /// <typeparam name="T">Тип данных успешного результата.</typeparam>
        /// <param name="controller">Контроллер, формирующий HTTP-ответ.</param>
        /// <param name="result">Результат выполнения операции сервиса.</param>
        /// <param name="onSuccess">Функция формирования ответа при успешном результате.</param>
        /// <returns>Результат действия контроллера.</returns>
        public static IActionResult ToActionResult<T>(
            this ControllerBase controller,
            ServiceResult<T> result,
            Func<T, IActionResult> onSuccess)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(onSuccess);

            if (!result.Success)
                return controller.ToProblemResult(result.Error);

            T data = result.Data
                ?? throw new InvalidOperationException("A successful result must contain data.");

            return onSuccess(data);
        }

        /// <summary>
        /// Преобразует результат сервиса без данных в успешный HTTP-ответ или описание ошибки.
        /// </summary>
        /// <param name="controller">Контроллер, формирующий HTTP-ответ.</param>
        /// <param name="result">Результат выполнения операции сервиса.</param>
        /// <param name="onSuccess">Функция формирования ответа при успешном результате.</param>
        /// <returns>Результат действия контроллера.</returns>
        public static IActionResult ToActionResult(
            this ControllerBase controller,
            ServiceResult result,
            Func<IActionResult> onSuccess)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(onSuccess);

            if (!result.Success)
                return controller.ToProblemResult(result.Error);

            return onSuccess();
        }

        /// <summary>
        /// Преобразует ошибку сервисного слоя в ответ формата <see cref="ProblemDetails"/>.
        /// </summary>
        /// <param name="controller">Контроллер, формирующий HTTP-ответ.</param>
        /// <param name="error">Ошибка сервисного слоя.</param>
        /// <returns>Ответ с соответствующим HTTP-статусом и описанием ошибки.</returns>
        private static ObjectResult ToProblemResult(this ControllerBase controller, ServiceError? error)
        {
            ArgumentNullException.ThrowIfNull(error);

            int statusCode = error.Type switch
            {
                ServiceErrorType.Validation => StatusCodes.Status400BadRequest,
                ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
                ServiceErrorType.Internal => StatusCodes.Status500InternalServerError,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(error.Type),
                    error.Type,
                    "Unknown service error type.")
            };

            return controller.Problem(
                statusCode: statusCode,
                title: "Request processing failed",
                detail: error.Message);
        }
    }
}
