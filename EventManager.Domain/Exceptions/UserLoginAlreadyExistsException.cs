namespace EventManager.Domain.Exceptions
{
    /// <summary>Сообщает о попытке создать пользователя с занятым логином.</summary>
    public class UserLoginAlreadyExistsException : Exception
    {
        /// <summary>Создаёт исключение о конфликте логина без раскрытия учётных данных.</summary>
        public UserLoginAlreadyExistsException() : base("The login is already in use.")
        {
        }
    }
}
