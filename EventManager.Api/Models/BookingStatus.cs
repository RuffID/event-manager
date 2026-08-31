namespace EventManager.Api.Models
{
    /// <summary>
    /// Определяет состояние бронирования.
    /// </summary>
    public enum BookingStatus
    {
        /// <summary>Бронь создана и ожидает обработки.</summary>
        Pending,

        /// <summary>Бронь подтверждена.</summary>
        Confirmed,

        /// <summary>Бронь отклонена.</summary>
        Rejected
    }
}
