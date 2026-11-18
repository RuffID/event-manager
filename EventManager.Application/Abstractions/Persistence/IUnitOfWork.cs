namespace EventManager.Application.Abstractions.Persistence
{
    /// <summary>Фиксирует все накопленные изменения общего контекста текущего scope.</summary>
    public interface IUnitOfWork
    {
        /// <summary>Сохраняет связанные изменения текущего контекста одним вызовом.</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
