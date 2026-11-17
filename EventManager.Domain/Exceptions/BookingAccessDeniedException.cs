namespace EventManager.Domain.Exceptions
{
    /// <summary>Представляет ошибку отмены бронирования без необходимых прав.</summary>
    public class BookingAccessDeniedException : Exception
    {
        /// <summary>Создаёт исключение с причиной отказа в отмене бронирования.</summary>
        public BookingAccessDeniedException()
            : base("The user is not allowed to cancel this booking.")
        {
        }
    }
}
