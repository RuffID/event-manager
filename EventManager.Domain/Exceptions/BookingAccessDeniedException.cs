namespace EventManager.Domain.Exceptions
{
    /// <summary>Представляет ошибку доступа к бронированию без необходимых прав.</summary>
    public class BookingAccessDeniedException : Exception
    {
        /// <summary>Создаёт исключение с причиной отказа в отмене бронирования.</summary>
        public BookingAccessDeniedException()
            : this("The user is not allowed to cancel this booking.")
        {
        }

        /// <summary>Создаёт исключение с причиной отказа в запрошенной операции с бронированием.</summary>
        /// <param name="message">Описание запрещённой операции.</param>
        public BookingAccessDeniedException(string message)
            : base(message)
        {
        }
    }
}
