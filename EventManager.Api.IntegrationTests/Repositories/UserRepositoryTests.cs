using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Domain.Exceptions;
using EventManager.Infrastructure.DataAccess;
using EventManager.Infrastructure.DataAccess.Repositories;
using EventManager.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace EventManager.Api.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.NAME)]
public class UserRepositoryTests(PostgresFixture database)
{
    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task Add_StoresUserOnlyAfterCommitAndFindsByLoginWithoutTracking(UserRole role)
    {
        // Arrange
        await database.ResetAsync();
        PasswordHashingService hasher = new PasswordHashingService();
        string passwordHash = hasher.Hash("private-password");
        User user = new User(Guid.NewGuid(), "learner", passwordHash, role);
        await using AppDbContext write = database.CreateContext();
        UserRepository repository = new UserRepository(write);

        // Act
        repository.Add(user);

        // Assert
        await using (AppDbContext before = database.CreateContext())
        {
            Assert.False(await before.Users.AnyAsync(TestContext.Current.CancellationToken));
        }

        // Act
        int saved = await new EfUnitOfWork(write).SaveChangesAsync(TestContext.Current.CancellationToken);
        await using AppDbContext read = database.CreateContext();
        User? found = await new UserRepository(read).GetByLoginAsync(user.Login, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, saved);
        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal(user.Login, found.Login);
        Assert.Equal(role, found.Role);
        Assert.Equal(passwordHash, found.PasswordHash);
        Assert.NotEqual("private-password", found.PasswordHash);
        Assert.True(hasher.Verify("private-password", found.PasswordHash));
        Assert.Empty(read.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByLogin_ReturnsNullForUnknownLogin()
    {
        // Arrange
        await database.ResetAsync();
        await using AppDbContext context = database.CreateContext();
        User existing = TestData.CreateUser();
        UserRepository repository = new UserRepository(context);
        repository.Add(existing);
        await new EfUnitOfWork(context).SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        User? missing = await repository.GetByLoginAsync("missing-login", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(missing);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Single(await context.Users.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UniqueLogin_RejectsSecondInsertEvenWhenBothPrechecksPassed()
    {
        // Arrange
        await database.ResetAsync();
        await using AppDbContext first = database.CreateContext();
        await using AppDbContext second = database.CreateContext();
        UserRepository firstRepository = new UserRepository(first);
        UserRepository secondRepository = new UserRepository(second);
        const string LOGIN = "same-login";
        Assert.Null(await firstRepository.GetByLoginAsync(LOGIN, TestContext.Current.CancellationToken));
        Assert.Null(await secondRepository.GetByLoginAsync(LOGIN, TestContext.Current.CancellationToken));
        User expected = new User(Guid.NewGuid(), LOGIN, "first-hash");
        firstRepository.Add(expected);
        secondRepository.Add(new User(Guid.NewGuid(), LOGIN, "second-hash"));
        await new EfUnitOfWork(first).SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        Func<Task> action = () => new EfUnitOfWork(second).SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<UserLoginAlreadyExistsException>(action);
        await using AppDbContext verify = database.CreateContext();

        User stored = Assert.Single(await verify.Users.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expected.Id, stored.Id);
        Assert.Equal(expected.PasswordHash, stored.PasswordHash);
    }

    [Fact]
    public async Task Booking_RejectsUnknownOwnerWithUserForeignKeyViolation()
    {
        // Arrange
        await database.ResetAsync();
        Event existing = TestData.CreateEvent();
        await TestData.SeedAsync(database, [existing]);
        await using AppDbContext write = database.CreateContext();
        new BookingRepository(write).Add(new Booking(existing.Id, Guid.NewGuid()));

        // Act
        Func<Task> action = () => new EfUnitOfWork(write).SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(action);
        PostgresException postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal("FK_bookings_users_user_id", postgres.ConstraintName);
        await using AppDbContext verify = database.CreateContext();

        Assert.Empty(await verify.Bookings.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        TestData.AssertEvent(existing, await verify.Events.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
    }
}
