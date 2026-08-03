namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет описание ошибки, возникшей при выполнении операции сервиса.
    /// </summary>
    /// <param name="type">Категория ошибки.</param>
    /// <param name="message">Текст ошибки.</param>
    public class ServiceError(ServiceErrorType type, string message)
    {
        /// <summary>Получает категорию ошибки.</summary>
        public ServiceErrorType Type { get; } = type;

        /// <summary>Получает текст ошибки.</summary>
        public string Message { get; } = message;
    }
}
