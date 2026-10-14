namespace EventManager.Api.DataAccess
{
    /// <summary>Фиксирует все накопленные изменения общего контекста текущего scope.</summary>
    public interface IUnitOfWork
    {
        /// <summary>Сохраняет связанные изменения событий и бронирований одним вызовом.</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
