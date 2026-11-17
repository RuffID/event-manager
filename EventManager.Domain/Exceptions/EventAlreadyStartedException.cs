namespace EventManager.Domain.Exceptions
{
    /// <summary>Представляет ошибку бронирования уже начавшегося события.</summary>
    public class EventAlreadyStartedException : Exception
    {
        /// <summary>Создаёт исключение с причиной отказа в бронировании.</summary>
        public EventAlreadyStartedException()
            : base("An event that has already started cannot be booked.")
        {
        }
    }
}
