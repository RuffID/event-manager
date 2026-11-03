using EventManager.Application.Abstractions.Persistence;

namespace EventManager.Api.Tests.Services;

internal class RecordingUnitOfWork : IUnitOfWork
{
    public List<CancellationToken> SavedTokens { get; } = [];
    public Exception? SaveError { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SavedTokens.Add(cancellationToken);
        if (SaveError is not null)
        {
            return Task.FromException<int>(SaveError);
        }

        return Task.FromResult(1);
    }
}
