namespace EventManager.Application.Models.Dtos
{
    /// <summary>Содержит JWT, выданный после успешного входа.</summary>
    public class TokenDto
    {
        /// <summary>Получает или задаёт подписанный токен доступа.</summary>
        public string Token { get; set; } = string.Empty;
    }
}
