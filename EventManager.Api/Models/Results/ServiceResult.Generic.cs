namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет результат операции сервиса с данными в ответе.
    /// </summary>
    /// <typeparam name="T">Тип данных результата.</typeparam>
    public class ServiceResult<T>
    {
        /// <summary>Получает HTTP-код результата операции.</summary>
        public int StatusCode { get; }

        /// <summary>Получает значение, указывающее на успешность операции.</summary>
        public bool Success { get; }

        /// <summary>Получает данные результата или <see langword="null"/> при ошибке.</summary>
        public T? Data { get; }

        /// <summary>Получает информацию об ошибке или <see langword="null"/> при успешной операции.</summary>
        public ServiceError? Error { get; }

        private ServiceResult(bool success, int statusCode, T? data, ServiceError? error)
        {
            Success = success;
            StatusCode = statusCode;
            Data = data;
            Error = error;
        }

        /// <summary>Создаёт успешный результат операции с данными.</summary>
        /// <param name="data">Данные результата.</param>
        /// <param name="statusCode">HTTP-код успешного ответа.</param>
        /// <returns>Успешный результат операции с данными.</returns>
        public static ServiceResult<T> Succeed(T data, int statusCode = 200)
        {
            return new ServiceResult<T>(true, statusCode, data, null);
        }

        /// <summary>Создаёт результат неуспешной операции.</summary>
        /// <param name="statusCode">HTTP-код ответа с ошибкой.</param>
        /// <param name="message">Текст ошибки.</param>
        /// <returns>Неуспешный результат операции с описанием ошибки.</returns>
        public static ServiceResult<T> Fail(int statusCode, string message)
        {
            return new ServiceResult<T>(false, statusCode, default, new ServiceError(message));
        }
    }
}
