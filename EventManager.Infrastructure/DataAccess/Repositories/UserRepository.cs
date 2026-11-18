using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Infrastructure.DataAccess.Repositories
{
    /// <summary>Выполняет поиск и добавление пользователей в общем scoped-контексте.</summary>
    /// <param name="context">Контекст текущей единицы работы.</param>
    public class UserRepository(AppDbContext context) : IUserRepository
    {
        /// <inheritdoc />
        public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken)
        {
            return context.Users.AsNoTracking()
                .SingleOrDefaultAsync(user => user.Login == login, cancellationToken);
        }

        /// <inheritdoc />
        public void Add(User user)
        {
            context.Users.Add(user);
        }
    }
}
