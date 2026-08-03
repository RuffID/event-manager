namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Представляет описание ошибки, возникшей при выполнении операции сервиса.
    /// </summary>
    /// <param name="message">Текст ошибки.</param>
    public class ServiceError(string message)
    {
        /// <summary>Получает текст ошибки.</summary>
        public string Message { get; } = message;
    }
}
