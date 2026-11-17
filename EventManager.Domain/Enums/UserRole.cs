namespace EventManager.Domain.Enums
{
    /// <summary>Определяет роль пользователя в системе.</summary>
    public enum UserRole
    {
        /// <summary>Пользователь, который бронирует события и отменяет собственные брони.</summary>
        User,

        /// <summary>Администратор, который управляет событиями и отменяет любые брони.</summary>
        Admin
    }
}
