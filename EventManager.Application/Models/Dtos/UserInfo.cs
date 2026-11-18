using EventManager.Domain.Enums;

namespace EventManager.Application.Models.Dtos
{
    /// <summary>Возвращает данные пользователя без пароля и его хеша.</summary>
    public class UserInfo
    {
        /// <summary>Получает или задаёт идентификатор пользователя.</summary>
        public Guid Id { get; set; }

        /// <summary>Получает или задаёт логин.</summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>Получает или задаёт роль.</summary>
        public UserRole Role { get; set; }
    }
}
