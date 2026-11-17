namespace EventManager.Infrastructure.Security
{
    /// <summary>Содержит параметры выпуска и последующей проверки JWT.</summary>
    public class JwtOptions
    {
        /// <summary>Имя раздела конфигурации JWT.</summary>
        public const string SECTION_NAME = "Jwt";

        /// <summary>Получает или задаёт издателя токена.</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Получает или задаёт API, которому предназначен токен.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>Получает или задаёт ключ подписи в формате Base64.</summary>
        public string SigningKeyBase64 { get; set; } = string.Empty;

        /// <summary>Получает или задаёт время жизни токена в минутах.</summary>
        public int LifetimeMinutes { get; set; }
    }
}
