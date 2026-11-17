using EventManager.Application.Abstractions.Persistence;
using EventManager.Application.Abstractions.Security;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EventManager.Application.Tests.Services.Users;

public class UserServiceTests
{
    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task Register_SavesHashedPasswordAndReturnsSafeUserData(UserRole role)
    {
        // Arrange
        UserRepositoryStub users = new UserRepositoryStub();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        PasswordHasherStub hasher = new PasswordHasherStub();
        TokenGeneratorStub tokens = new TokenGeneratorStub();
        RecordingLogger<UserService> logger = new RecordingLogger<UserService>();
        UserService service = new UserService(users, work, hasher, tokens, logger);
        RegisterUserDto dto = new RegisterUserDto { Login = "  learner  ", Password = "private-password", Role = role };
        CancellationToken token = TestContext.Current.CancellationToken;

        // Act
        ServiceResult<UserInfo> result = await service.RegisterAsync(dto, token);

        // Assert
        Assert.True(result.Success);
        User saved = Assert.IsType<User>(users.Added);
        UserInfo data = Assert.IsType<UserInfo>(result.Data);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal("learner", saved.Login);
        Assert.Equal(hasher.HashResult, saved.PasswordHash);
        Assert.NotEqual(dto.Password, saved.PasswordHash);
        Assert.Equal(dto.Password, hasher.HashedPassword);
        Assert.Equal(role, saved.Role);
        Assert.Equal(saved.Id, data.Id);
        Assert.Equal(saved.Login, data.Login);
        Assert.Equal(role, data.Role);
        Assert.Equal(token, Assert.Single(work.SavedTokens));
        Assert.Equal(("learner", token), Assert.Single(users.Reads));
        Assert.Null(tokens.ReceivedUser);
        Assert.DoesNotContain(typeof(UserInfo).GetProperties(), property => property.Name.Contains("Password"));
        string message = Assert.Single(logger.Entries).Message;
        Assert.Contains(saved.Id.ToString(), message);
        Assert.DoesNotContain(dto.Password, message);
        Assert.DoesNotContain(hasher.HashResult, message);
        Assert.DoesNotContain(saved.Login, message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_DuplicateLoginReturnsConflictBeforeOrDuringSave(bool concurrentInsert)
    {
        // Arrange
        UserRepositoryStub users = new UserRepositoryStub();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        if (concurrentInsert)
        {
            work.SaveError = new UserLoginAlreadyExistsException();
        }
        else
        {
            users.Existing = new User(Guid.NewGuid(), "learner", "stored-hash");
        }

        RecordingLogger<UserService> logger = new RecordingLogger<UserService>();
        UserService service = new UserService(users, work, new PasswordHasherStub(), new TokenGeneratorStub(), logger);

        // Act
        ServiceResult<UserInfo> result = await service.RegisterAsync(
            new RegisterUserDto { Login = "learner", Password = "private-password" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(ServiceErrorType.Conflict, Assert.IsType<ServiceError>(result.Error).Type);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Information);
        if (!concurrentInsert)
        {
            Assert.Null(users.Added);
            Assert.Empty(work.SavedTokens);
        }
    }

    [Theory]
    [InlineData("", "password", UserRole.User)]
    [InlineData("learner", " ", UserRole.User)]
    [InlineData("learner", "password", (UserRole)99)]
    public async Task Register_InvalidDataDoesNotReadOrSave(string login, string password, UserRole role)
    {
        // Arrange
        UserRepositoryStub users = new UserRepositoryStub();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        UserService service = new UserService(users, work, new PasswordHasherStub(), new TokenGeneratorStub(), new RecordingLogger<UserService>());

        // Act
        ServiceResult<UserInfo> result = await service.RegisterAsync(
            new RegisterUserDto { Login = login, Password = password, Role = role }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, Assert.IsType<ServiceError>(result.Error).Type);
        Assert.Empty(users.Reads);
        Assert.Null(users.Added);
        Assert.Empty(work.SavedTokens);
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task Login_VerifiesStoredHashAndGeneratesTokenFromStoredUser(UserRole role)
    {
        // Arrange
        User user = new User(Guid.NewGuid(), "learner", "stored-hash", role);
        UserRepositoryStub users = new UserRepositoryStub { Existing = user };
        PasswordHasherStub hasher = new PasswordHasherStub { AcceptPassword = true };
        TokenGeneratorStub tokens = new TokenGeneratorStub();
        RecordingUnitOfWork work = new RecordingUnitOfWork();
        RecordingLogger<UserService> logger = new RecordingLogger<UserService>();
        UserService service = new UserService(users, work, hasher, tokens, logger);

        // Act
        ServiceResult<TokenDto> result = await service.LoginAsync(
            new LoginDto { Login = " learner ", Password = "private-password" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("test-access-token", Assert.IsType<TokenDto>(result.Data).Token);
        Assert.Same(user, tokens.ReceivedUser);
        Assert.Equal(("private-password", user.PasswordHash), hasher.Verified);
        Assert.Null(hasher.HashedPassword);
        Assert.Empty(work.SavedTokens);
        string message = Assert.Single(logger.Entries).Message;
        Assert.Contains(user.Id.ToString(), message);
        Assert.DoesNotContain("private-password", message);
        Assert.DoesNotContain(user.PasswordHash, message);
        Assert.DoesNotContain("test-access-token", message);
    }

    [Fact]
    public async Task Login_UnknownLoginAndWrongPasswordReturnSameErrorWithoutToken()
    {
        // Arrange
        UserRepositoryStub users = new UserRepositoryStub();
        TokenGeneratorStub tokens = new TokenGeneratorStub();
        RecordingLogger<UserService> logger = new RecordingLogger<UserService>();
        UserService service = new UserService(users, new RecordingUnitOfWork(), new PasswordHasherStub(), tokens, logger);
        LoginDto dto = new LoginDto { Login = "learner", Password = "wrong-password" };

        // Act
        ServiceResult<TokenDto> missing = await service.LoginAsync(dto, TestContext.Current.CancellationToken);
        users.Existing = new User(Guid.NewGuid(), "learner", "stored-hash");
        ServiceResult<TokenDto> wrong = await service.LoginAsync(dto, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(missing.Success);
        Assert.False(wrong.Success);
        ServiceError first = Assert.IsType<ServiceError>(missing.Error);
        ServiceError second = Assert.IsType<ServiceError>(wrong.Error);
        Assert.Equal(ServiceErrorType.Unauthorized, first.Type);
        Assert.Equal(first.Type, second.Type);
        Assert.Equal(first.Message, second.Message);
        Assert.Null(missing.Data);
        Assert.Null(wrong.Data);
        Assert.Null(tokens.ReceivedUser);
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.All(logger.Entries, entry => Assert.DoesNotContain(dto.Password, entry.Message));
    }

    [Fact]
    public async Task Register_UnexpectedSaveFailurePropagatesWithoutSuccessLog()
    {
        // Arrange
        InvalidOperationException failure = new InvalidOperationException("Storage unavailable.");
        RecordingUnitOfWork work = new RecordingUnitOfWork { SaveError = failure };
        RecordingLogger<UserService> logger = new RecordingLogger<UserService>();
        UserService service = new UserService(new UserRepositoryStub(), work, new PasswordHasherStub(), new TokenGeneratorStub(), logger);

        // Act
        Func<Task> action = () => service.RegisterAsync(
            new RegisterUserDto { Login = "learner", Password = "private-password" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(action));
        Assert.Empty(logger.Entries);
    }

    private class UserRepositoryStub : IUserRepository
    {
        public User? Existing { get; set; }
        public User? Added { get; private set; }
        public List<(string Login, CancellationToken Token)> Reads { get; } = [];

        public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken)
        {
            Reads.Add((login, cancellationToken));

            return Task.FromResult(Existing);
        }

        public void Add(User user)
        {
            Added = user;
        }
    }

    private class PasswordHasherStub : IPasswordHasher
    {
        public string HashResult { get; } = "test-password-hash";
        public string? HashedPassword { get; private set; }
        public bool AcceptPassword { get; set; }
        public (string Password, string Hash)? Verified { get; private set; }

        public string Hash(string password)
        {
            HashedPassword = password;

            return HashResult;
        }

        public bool Verify(string password, string passwordHash)
        {
            Verified = (password, passwordHash);

            return AcceptPassword;
        }
    }

    private class TokenGeneratorStub : IJwtTokenGenerator
    {
        public User? ReceivedUser { get; private set; }

        public string Generate(User user)
        {
            ReceivedUser = user;

            return "test-access-token";
        }
    }
}
