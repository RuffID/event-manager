namespace EventManager.Api.DataAccess
{
    /// <summary>Сохраняет изменения общего scoped-контекста, которым владеет DI.</summary>
    /// <param name="context">Контекст текущей единицы работы.</param>
    public class EfUnitOfWork(AppDbContext context) : IUnitOfWork
    {
        /// <inheritdoc />
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return context.SaveChangesAsync(cancellationToken);
        }
    }
}
