namespace EventManager.Application.Abstractions.Security
{
    /// <summary>Определяет хеширование и проверку паролей без зависимости от алгоритма.</summary>
    public interface IPasswordHasher
    {
        /// <summary>Создаёт хеш пароля для сохранения в учётной записи.</summary>
        /// <param name="password">Пароль пользователя.</param>
        /// <returns>Хеш с параметрами, необходимыми для последующей проверки.</returns>
        string Hash(string password);

        /// <summary>Проверяет соответствие пароля сохранённому хешу.</summary>
        /// <param name="password">Проверяемый пароль.</param>
        /// <param name="passwordHash">Сохранённый хеш пароля.</param>
        /// <returns>Признак соответствия пароля хешу.</returns>
        bool Verify(string password, string passwordHash);
    }
}
