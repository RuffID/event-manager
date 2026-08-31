namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет описание ошибки, возникшей при выполнении операции сервиса.
    /// </summary>
    public class ServiceError
    {
        /// <summary>Получает категорию ошибки.</summary>
        public ServiceErrorType Type { get; }

        /// <summary>Получает текст ошибки.</summary>
        public string Message { get; }

        /// <summary>Создаёт описание ошибки сервисного слоя.</summary>
        /// <param name="type">Категория ошибки.</param>
        /// <param name="message">Текст ошибки.</param>
        public ServiceError(ServiceErrorType type, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Error message must not be empty.", nameof(message));

            Type = type;
            Message = message;
        }
    }
}
