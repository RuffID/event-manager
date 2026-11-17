namespace EventManager.Domain.Exceptions
{
    /// <summary>Представляет ошибку превышения лимита активных бронирований пользователя.</summary>
    public class ActiveBookingLimitExceededException : Exception
    {
        /// <summary>Получает лимит активных бронирований.</summary>
        public int Limit { get; }

        /// <summary>Создаёт исключение с указанием достигнутого лимита.</summary>
        /// <param name="limit">Максимальное количество активных бронирований.</param>
        public ActiveBookingLimitExceededException(int limit)
            : base($"The active booking limit of {limit} has been reached.")
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

            Limit = limit;
        }
    }
}
