namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет результат операции сервиса с данными в ответе.
    /// </summary>
    /// <typeparam name="T">Тип данных результата.</typeparam>
    public class ServiceResult<T>
    {
        /// <summary>Получает значение, указывающее на успешность операции.</summary>
        public bool Success { get; }

        /// <summary>Получает данные результата или <see langword="null"/> при ошибке.</summary>
        public T? Data { get; }

        /// <summary>Получает информацию об ошибке или <see langword="null"/> при успешной операции.</summary>
        public ServiceError? Error { get; }

        private ServiceResult(bool success, T? data, ServiceError? error)
        {
            Success = success;
            Data = data;
            Error = error;
        }

        /// <summary>Создаёт успешный результат операции с данными.</summary>
        /// <param name="data">Данные результата.</param>
        /// <returns>Успешный результат операции с данными.</returns>
        public static ServiceResult<T> Succeed(T data)
        {
            return new ServiceResult<T>(true, data, null);
        }

        /// <summary>Создаёт результат неуспешной операции.</summary>
        /// <param name="errorType">Категория ошибки.</param>
        /// <param name="message">Текст ошибки.</param>
        /// <returns>Неуспешный результат операции с описанием ошибки.</returns>
        public static ServiceResult<T> Fail(ServiceErrorType errorType, string message)
        {
            return new ServiceResult<T>(false, default, new ServiceError(errorType, message));
        }
    }
}
