namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет результат операции сервиса без данных в ответе.
    /// </summary>
    public class ServiceResult
    {
        /// <summary>Получает HTTP-код результата операции.</summary>
        public int StatusCode { get; }

        /// <summary>Получает значение, указывающее на успешность операции.</summary>
        public bool Success { get; }

        /// <summary>Получает информацию об ошибке или <see langword="null"/> при успешной операции.</summary>
        public ServiceError? Error { get; }

        private ServiceResult(bool success, int statusCode, ServiceError? error = null)
        {
            Success = success;
            StatusCode = statusCode;
            Error = error;
        }

        /// <summary>Создаёт успешный результат операции без данных.</summary>
        /// <param name="statusCode">HTTP-код успешного ответа.</param>
        /// <returns>Успешный результат операции.</returns>
        public static ServiceResult Succeed(int statusCode = 200) 
        {
            return new ServiceResult(true, statusCode);
        }

        /// <summary>Создаёт результат неуспешной операции.</summary>
        /// <param name="statusCode">HTTP-код ответа с ошибкой.</param>
        /// <param name="message">Текст ошибки.</param>
        /// <returns>Неуспешный результат операции с описанием ошибки.</returns>
        public static ServiceResult Fail(int statusCode, string message)
        {
            return new ServiceResult(false, statusCode, new ServiceError(message));
        }
    }
}
