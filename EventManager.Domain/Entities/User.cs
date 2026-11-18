using EventManager.Domain.Enums;

namespace EventManager.Domain.Entities
{
    /// <summary>Представляет пользователя с учётными данными и ролью.</summary>
    public class User
    {
        /// <summary>Получает уникальный идентификатор пользователя.</summary>
        public Guid Id { get; private set; }

        /// <summary>Получает логин пользователя.</summary>
        public string Login { get; private set; }

        /// <summary>Получает хеш пароля пользователя.</summary>
        public string PasswordHash { get; private set; }

        /// <summary>Получает роль пользователя.</summary>
        public UserRole Role { get; private set; }

        /// <summary>Создаёт экземпляр для материализации EF Core.</summary>
        private User()
        {
            Login = null!;
            PasswordHash = null!;
        }

        /// <summary>Создаёт пользователя с готовым хешем пароля.</summary>
        /// <param name="id">Уникальный идентификатор пользователя.</param>
        /// <param name="login">Логин пользователя.</param>
        /// <param name="passwordHash">Хеш пароля, подготовленный компонентом безопасности.</param>
        /// <param name="role">Роль пользователя.</param>
        public User(Guid id, string login, string passwordHash, UserRole role = UserRole.User)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("User identifier must not be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("User login must not be empty.", nameof(login));
            }

            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Password hash must not be empty.", nameof(passwordHash));
            }

            if (!Enum.IsDefined(role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), "User role is not supported.");
            }

            Id = id;
            Login = login;
            PasswordHash = passwordHash;
            Role = role;
        }
    }
}
