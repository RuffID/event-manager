using EventManager.Api.Models.Results;
using Microsoft.AspNetCore.Mvc;

namespace EventManager.Api.Extensions
{
    internal static class ControllerProblemExtensions
    {
        public static ObjectResult ToProblemResult(this ControllerBase controller, ServiceError? error)
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
