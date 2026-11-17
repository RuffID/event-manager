using EventManager.Application.Abstractions.Persistence;
using EventManager.Application.Abstractions.Security;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Domain.Entities;
using EventManager.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace EventManager.Application.Services
{
    /// <summary>Регистрирует пользователей и проверяет учётные данные перед выдачей JWT.</summary>
    /// <param name="users">Репозиторий пользователей.</param>
    /// <param name="unitOfWork">Общая граница сохранения изменений.</param>
    /// <param name="passwordHasher">Компонент хеширования и проверки паролей.</param>
    /// <param name="tokenGenerator">Генератор подписанных JWT.</param>
    /// <param name="logger">Сервис журналирования.</param>
    public class UserService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ILogger<UserService> logger) : IUserService
    {
        /// <inheritdoc />
        public async Task<ServiceResult<UserInfo>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return ServiceResult<UserInfo>.Fail(ServiceErrorType.Validation, "Login and password are required.");
            }

            if (!Enum.IsDefined(dto.Role))
            {
                return ServiceResult<UserInfo>.Fail(ServiceErrorType.Validation, "User role is not supported.");
            }

            string login = dto.Login.Trim();
            User? existing = await users.GetByLoginAsync(login, cancellationToken);
            if (existing is not null)
            {
                return ServiceResult<UserInfo>.Fail(ServiceErrorType.Conflict, "The login is already in use.");
            }

            string passwordHash = passwordHasher.Hash(dto.Password);
            User user = new User(Guid.NewGuid(), login, passwordHash, dto.Role);
            users.Add(user);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UserLoginAlreadyExistsException)
            {
                return ServiceResult<UserInfo>.Fail(ServiceErrorType.Conflict, "The login is already in use.");
            }

            logger.LogInformation("User {UserId} registered with role {Role}.", user.Id, user.Role);

            return ServiceResult<UserInfo>.Succeed(new UserInfo
            {
                Id = user.Id,
                Login = user.Login,
                Role = user.Role
            });
        }

        /// <inheritdoc />
        public async Task<ServiceResult<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            cancellationToken.ThrowIfCancellationRequested();
            const string INVALID_CREDENTIALS = "Invalid login or password.";
            if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return ServiceResult<TokenDto>.Fail(ServiceErrorType.Unauthorized, INVALID_CREDENTIALS);
            }

            User? user = await users.GetByLoginAsync(dto.Login.Trim(), cancellationToken);
            if (user is null)
            {
                logger.LogWarning("Login rejected: invalid credentials.");

                return ServiceResult<TokenDto>.Fail(ServiceErrorType.Unauthorized, INVALID_CREDENTIALS);
            }

            if (!passwordHasher.Verify(dto.Password, user.PasswordHash))
            {
                logger.LogWarning("Login rejected: invalid credentials.");

                return ServiceResult<TokenDto>.Fail(ServiceErrorType.Unauthorized, INVALID_CREDENTIALS);
            }

            string token = tokenGenerator.Generate(user);
            logger.LogInformation("User {UserId} logged in.", user.Id);

            return ServiceResult<TokenDto>.Succeed(new TokenDto { Token = token });
        }
    }
}
