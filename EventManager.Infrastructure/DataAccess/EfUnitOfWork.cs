using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Exceptions;
using EventManager.Infrastructure.DataAccess.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventManager.Infrastructure.DataAccess
{
    /// <summary>Сохраняет изменения общего scoped-контекста, которым владеет DI.</summary>
    /// <param name="context">Контекст текущей единицы работы.</param>
    public class EfUnitOfWork(AppDbContext context) : IUnitOfWork
    {
        /// <inheritdoc />
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: UserConfiguration.LOGIN_INDEX_NAME
                })
            {
                throw new UserLoginAlreadyExistsException();
            }
        }
    }
}
