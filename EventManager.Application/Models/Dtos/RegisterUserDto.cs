using System.ComponentModel.DataAnnotations;
using EventManager.Domain.Enums;

namespace EventManager.Application.Models.Dtos
{
    /// <summary>Содержит данные для регистрации учебной учётной записи.</summary>
    public class RegisterUserDto
    {
        /// <summary>Получает или задаёт логин.</summary>
        [Required]
        public string Login { get; set; } = string.Empty;

        /// <summary>Получает или задаёт пароль, который сохраняется только в виде хеша.</summary>
        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;

        /// <summary>Получает или задаёт роль; выбор Admin разрешён ТЗ для учебного тестирования.</summary>
        [EnumDataType(typeof(UserRole))]
        public UserRole Role { get; set; } = UserRole.User;
    }
}
