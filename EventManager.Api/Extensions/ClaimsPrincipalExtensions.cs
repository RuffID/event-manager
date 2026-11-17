using System.Security.Claims;

namespace EventManager.Api.Extensions
{
    /// <summary>Читает идентификатор из подтверждённой личности текущего пользователя.</summary>
    internal static class ClaimsPrincipalExtensions
    {
        /// <summary>Проверяет аутентификацию и получает непустой идентификатор пользователя.</summary>
        /// <param name="principal">Личность, подготовленная серверной аутентификацией.</param>
        /// <param name="userId">Идентификатор из claims.</param>
        public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
        {
            userId = Guid.Empty;
            if (principal.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (value is null)
            {
                value = principal.FindFirstValue("sub");
            }

            return Guid.TryParse(value, out userId) && userId != Guid.Empty;
        }
    }
}
