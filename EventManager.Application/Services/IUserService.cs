using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;

namespace EventManager.Application.Services
{
    /// <summary>Определяет сценарии регистрации и входа пользователей.</summary>
    public interface IUserService
    {
        /// <summary>Регистрирует пользователя, сохраняя только хеш пароля.</summary>
        Task<ServiceResult<UserInfo>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);

        /// <summary>Проверяет учётные данные и возвращает JWT.</summary>
        Task<ServiceResult<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    }
}
