using System.Collections.Concurrent;
using EventManager.Api.BackgroundServices;
using EventManager.Api.DataAccess;
using EventManager.Api.Tests.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EventManager.Api.Tests.BackgroundServices;

public class BookingProcessorScopeTests
{
    [Fact]
    public async Task ParallelProcessing_UsesAndDisposesIndependentContexts()
    {
        // Arrange
        const int BOOKING_COUNT = 3;
        CoordinatedSaveInterceptor interceptor = new CoordinatedSaveInterceptor(BOOKING_COUNT);
        using ServiceTestContext database = new ServiceTestContext(new ImmediateDelay(), interceptor);
        Event existing = CreateEvent(BOOKING_COUNT);
        Assert.True(existing.TryReserveSeats(BOOKING_COUNT));
        await database.SeedAsync(existing);
        Booking[] bookings = Enumerable.Range(0, BOOKING_COUNT).Select(_ => new Booking(existing.Id)).ToArray();
        database.Context.Bookings.AddRange(bookings);
        await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.Context.ChangeTracker.Clear();
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await database.BookingProcessor.ProcessPendingBookingsAsync(timeout.Token);

        // Assert
        Assert.Equal(BOOKING_COUNT, interceptor.Contexts.Count);
        Assert.Equal(BOOKING_COUNT, interceptor.Contexts.Values.Distinct().Count());
        Assert.All(interceptor.Contexts.Values, context =>
        {
            Assert.NotSame(database.Context, context);
            Assert.Throws<ObjectDisposedException>(() => context.Model);
        });
        Assert.Equal(bookings.Select(booking => booking.Id).Order(), interceptor.Contexts.Keys.Order());
        Assert.All(await database.Context.Bookings.ToListAsync(TestContext.Current.CancellationToken), booking =>
        {
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.ProcessedAt);
        });
        Assert.Equal(0, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
    }

    [Fact]
    public async Task SaveFailure_CompensatesInNewContextAndDisposesBothScopes()
    {
        // Arrange
        FailingConfirmationInterceptor interceptor = new FailingConfirmationInterceptor();
        using ServiceTestContext database = new ServiceTestContext(new ImmediateDelay(), interceptor);
        Event existing = CreateEvent(1);
        await database.SeedAsync(existing);
        await database.BookingService.CreateBookingAsync(existing.Id, TestContext.Current.CancellationToken);
        database.Context.ChangeTracker.Clear();

        // Act
        await database.BookingProcessor.ProcessPendingBookingsAsync(TestContext.Current.CancellationToken);

        // Assert
        AppDbContext failed = Assert.IsType<AppDbContext>(interceptor.FailedContext);
        AppDbContext compensation = Assert.IsType<AppDbContext>(interceptor.CompensationContext);
        Assert.NotSame(failed, compensation);
        Assert.NotSame(database.Context, failed);
        Assert.NotSame(database.Context, compensation);
        Assert.Throws<ObjectDisposedException>(() => failed.Model);
        Assert.Throws<ObjectDisposedException>(() => compensation.Model);
        Assert.Equal(CancellationToken.None, interceptor.CompensationToken);
        Booking stored = await database.Context.Bookings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BookingStatus.Rejected, stored.Status);
        Assert.NotNull(stored.ProcessedAt);
        Assert.Equal(1, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
    }

    private static Event CreateEvent(int seats)
    {
        DateTime startAt = DateTime.UtcNow.AddDays(1);

        return Event.Create("Scoped processing", null, startAt, startAt.AddHours(2), seats);
    }

    private class ImmediateDelay : IBookingProcessingDelay
    {
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    private class CoordinatedSaveInterceptor(int expectedCount) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> _allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentDictionary<Guid, AppDbContext> Contexts { get; } = new();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            AppDbContext context = (AppDbContext)eventData.Context!;
            Booking? booking = context.ChangeTracker.Entries<Booking>()
                .Where(entry => entry.State == EntityState.Modified)
                .Select(entry => entry.Entity)
                .SingleOrDefault();
            if (booking?.Status != BookingStatus.Confirmed)
            {
                return result;
            }

            Contexts.TryAdd(booking.Id, context);
            if (Contexts.Count == expectedCount)
            {
                _allStarted.TrySetResult(true);
            }

            // Все сохранения должны пересечься до освобождения любого из scopes.
            await _allStarted.Task.WaitAsync(cancellationToken);

            return result;
        }
    }

    private class FailingConfirmationInterceptor : SaveChangesInterceptor
    {
        public AppDbContext? FailedContext { get; private set; }
        public AppDbContext? CompensationContext { get; private set; }
        public CancellationToken? CompensationToken { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            AppDbContext context = (AppDbContext)eventData.Context!;
            Booking? booking = context.ChangeTracker.Entries<Booking>()
                .Where(entry => entry.State == EntityState.Modified)
                .Select(entry => entry.Entity)
                .SingleOrDefault();
            if (booking?.Status == BookingStatus.Confirmed)
            {
                FailedContext = context;
                throw new DbUpdateException("Confirmation failed.");
            }

            if (booking?.Status == BookingStatus.Rejected)
            {
                CompensationContext = context;
                CompensationToken = cancellationToken;
            }

            return ValueTask.FromResult(result);
        }
    }
}
