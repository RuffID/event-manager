using EventManager.Domain.Entities;

namespace EventManager.Application.Abstractions.Security
{
    /// <summary>Определяет выпуск токена для пользователя после успешного входа.</summary>
    public interface IJwtTokenGenerator
    {
        /// <summary>Создаёт подписанный токен по данным пользователя.</summary>
        /// <param name="user">Пользователь, учётные данные которого уже проверены.</param>
        /// <returns>Строка подписанного JWT.</returns>
        string Generate(User user);
    }
}
