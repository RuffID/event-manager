using EventManager.Domain.Entities;

namespace EventManager.Application.Abstractions.Persistence
{
    /// <summary>Предоставляет поиск и добавление пользователей без сохранения изменений.</summary>
    public interface IUserRepository
    {
        /// <summary>Находит пользователя по логину без отслеживания.</summary>
        Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken);

        /// <summary>Добавляет пользователя в общий контекст.</summary>
        void Add(User user);
    }
}
