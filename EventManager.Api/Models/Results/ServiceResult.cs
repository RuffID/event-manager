namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет результат операции сервиса без данных в ответе.
    /// </summary>
    public class ServiceResult
    {
        /// <summary>Получает значение, указывающее на успешность операции.</summary>
        public bool Success { get; }

        /// <summary>Получает информацию об ошибке или <see langword="null"/> при успешной операции.</summary>
        public ServiceError? Error { get; }

        private ServiceResult(bool success, ServiceError? error = null)
        {
            Success = success;
            Error = error;
        }

        /// <summary>Создаёт успешный результат операции без данных.</summary>
        /// <returns>Успешный результат операции.</returns>
        public static ServiceResult Succeed()
        {
            return new ServiceResult(true);
        }

        /// <summary>Создаёт результат неуспешной операции.</summary>
        /// <param name="errorType">Категория ошибки.</param>
        /// <param name="message">Текст ошибки.</param>
        /// <returns>Неуспешный результат операции с описанием ошибки.</returns>
        public static ServiceResult Fail(ServiceErrorType errorType, string message)
        {
            return new ServiceResult(false, new ServiceError(errorType, message));
        }
    }
}
