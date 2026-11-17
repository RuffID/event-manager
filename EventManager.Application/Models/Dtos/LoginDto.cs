using System.ComponentModel.DataAnnotations;

namespace EventManager.Application.Models.Dtos
{
    /// <summary>Содержит учётные данные для входа.</summary>
    public class LoginDto
    {
        /// <summary>Получает или задаёт логин.</summary>
        [Required]
        public string Login { get; set; } = string.Empty;

        /// <summary>Получает или задаёт пароль.</summary>
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
