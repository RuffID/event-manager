namespace EventManager.Api.Models.Results
{
    /// <summary>
    /// Определяет категорию ожидаемой ошибки сервисного слоя.
    /// </summary>
    public enum ServiceErrorType
    {
        /// <summary>Переданные данные не прошли проверку.</summary>
        Validation,

        /// <summary>Запрошенные данные не найдены.</summary>
        NotFound,

        /// <summary>Операцию не удалось выполнить из-за внутренней ошибки.</summary>
        Internal
    }
}
