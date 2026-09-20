namespace EventManager.Api.Exceptions
{
    /// <summary>
    /// Представляет ошибку бронирования события без доступных мест.
    /// </summary>
    public class NoAvailableSeatsException : Exception
    {
        /// <summary>Создаёт исключение с сообщением об отсутствии свободных мест.</summary>
        public NoAvailableSeatsException()
            : base("No available seats for this event")
        {
        }
    }
}
