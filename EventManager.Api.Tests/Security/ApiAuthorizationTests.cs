using System.Reflection;
using System.Text;
using EventManager.Api.Controllers;
using EventManager.Api.Extensions;
using EventManager.Application.Abstractions.Security;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using EventManager.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EventManager.Api.Tests.Security
{
    public class ApiAuthorizationTests
    {
        private const string ISSUER = "test-issuer";
        private const string AUDIENCE = "test-api";
        private const string SIGNING_KEY = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Theory]
        [InlineData(UserRole.User)]
        [InlineData(UserRole.Admin)]
        public async Task Authentication_AcceptsIssuedTokenAndPreservesUserIdAndRole(UserRole role)
        {
            // Arrange
            using ServiceProvider provider = CreateProvider();
            using IServiceScope scope = provider.CreateScope();
            User user = new User(Guid.NewGuid(), "student", "stored-hash", role);
            string token = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().Generate(user);
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, token);

            // Act
            AuthenticateResult result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);
            Assert.Equal(user.Id.ToString("D"), result.Principal.FindFirst("sub")?.Value);
            Assert.True(result.Principal.IsInRole(role.ToString()));
            Assert.False(result.Principal.IsInRole(role == UserRole.Admin ? "User" : "Admin"));
        }

        [Theory]
        [InlineData("issuer")]
        [InlineData("audience")]
        [InlineData("key")]
        [InlineData("expired")]
        [InlineData("not-yet-valid")]
        [InlineData("missing-subject")]
        [InlineData("invalid-subject")]
        [InlineData("empty-user-id")]
        [InlineData("algorithm")]
        [InlineData("unsigned")]
        public async Task Authentication_RejectsInvalidToken(string invalidPart)
        {
            // Arrange
            using ServiceProvider provider = CreateProvider();
            using IServiceScope scope = provider.CreateScope();
            DateTime now = DateTime.UtcNow;
            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
            {
                Issuer = ISSUER,
                Audience = AUDIENCE,
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = Guid.NewGuid().ToString("D"),
                    ["role"] = "Admin"
                },
                IssuedAt = now.AddMinutes(-2),
                NotBefore = now.AddMinutes(-2),
                Expires = now.AddMinutes(15),
                SigningCredentials = CreateCredentials(SIGNING_KEY, SecurityAlgorithms.HmacSha256)
            };
            switch (invalidPart)
            {
                case "issuer":
                    descriptor.Issuer = "other-issuer";
                    break;
                case "audience":
                    descriptor.Audience = "other-audience";
                    break;
                case "key":
                    descriptor.SigningCredentials = CreateCredentials(new string('x', 64), SecurityAlgorithms.HmacSha256);
                    break;
                case "expired":
                    descriptor.Expires = now.AddMinutes(-1);
                    break;
                case "not-yet-valid":
                    descriptor.NotBefore = now.AddMinutes(1);
                    break;
                case "missing-subject":
                    descriptor.Claims.Remove("sub");
                    break;
                case "invalid-subject":
                    descriptor.Claims["sub"] = "not-a-guid";
                    break;
                case "empty-user-id":
                    descriptor.Claims["sub"] = Guid.Empty.ToString("D");
                    break;
                case "algorithm":
                    descriptor.SigningCredentials = CreateCredentials(SIGNING_KEY, SecurityAlgorithms.HmacSha384);
                    break;
                case "unsigned":
                    descriptor.SigningCredentials = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidPart));
            }

            string token = new JsonWebTokenHandler().CreateToken(descriptor);
            DefaultHttpContext context = CreateContext(scope.ServiceProvider, token);

            // Act
            AuthenticateResult result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Failure);
        }

        [Theory]
        [InlineData(typeof(AuthController), nameof(AuthController.Register), null, 204)]
        [InlineData(typeof(AuthController), nameof(AuthController.Login), null, 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.GetEvents), null, 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.GetEvent), null, 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.CreateEvent), null, 401)]
        [InlineData(typeof(EventsController), nameof(EventsController.CreateEvent), "User", 403)]
        [InlineData(typeof(EventsController), nameof(EventsController.CreateEvent), "Admin", 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.UpdateEvent), null, 401)]
        [InlineData(typeof(EventsController), nameof(EventsController.UpdateEvent), "User", 403)]
        [InlineData(typeof(EventsController), nameof(EventsController.UpdateEvent), "Admin", 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.DeleteEvent), null, 401)]
        [InlineData(typeof(EventsController), nameof(EventsController.DeleteEvent), "User", 403)]
        [InlineData(typeof(EventsController), nameof(EventsController.DeleteEvent), "Admin", 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.CreateBooking), null, 401)]
        [InlineData(typeof(EventsController), nameof(EventsController.CreateBooking), "User", 204)]
        [InlineData(typeof(EventsController), nameof(EventsController.CreateBooking), "Admin", 204)]
        [InlineData(typeof(BookingsController), nameof(BookingsController.GetBooking), null, 401)]
        [InlineData(typeof(BookingsController), nameof(BookingsController.GetBooking), "User", 204)]
        [InlineData(typeof(BookingsController), nameof(BookingsController.GetBooking), "Admin", 204)]
        [InlineData(typeof(BookingsController), nameof(BookingsController.CancelBooking), null, 401)]
        [InlineData(typeof(BookingsController), nameof(BookingsController.CancelBooking), "User", 204)]
        [InlineData(typeof(BookingsController), nameof(BookingsController.CancelBooking), "Admin", 204)]
        public async Task Authorization_EnforcesEndpointAccess(Type controller, string action, string? role, int status)
        {
            // Arrange
            using ServiceProvider provider = CreateProvider();
            using IServiceScope scope = provider.CreateScope();
            string? token = null;
            if (role != null)
            {
                User user = new User(Guid.NewGuid(), "student", "stored-hash", Enum.Parse<UserRole>(role));
                token = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().Generate(user);
            }

            DefaultHttpContext context = CreateContext(scope.ServiceProvider, token);
            MethodInfo method = controller.GetMethod(action)!;
            object[] metadata = controller.GetCustomAttributes().Concat(method.GetCustomAttributes()).ToArray();
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), action));
            bool reachedEndpoint = false;
            ApplicationBuilder pipeline = new ApplicationBuilder(provider);
            pipeline.UseAuthentication();
            pipeline.UseAuthorization();
            pipeline.Run(httpContext =>
            {
                reachedEndpoint = true;
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

            // Act
            await pipeline.Build()(context);

            // Assert
            Assert.Equal(status, context.Response.StatusCode);
            Assert.Equal(status == StatusCodes.Status204NoContent, reachedEndpoint);
            if (status == StatusCodes.Status401Unauthorized)
            {
                Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
            }
        }

        private static ServiceProvider CreateProvider()
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = ISSUER,
                ["Jwt:Audience"] = AUDIENCE,
                ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SIGNING_KEY)),
                ["Jwt:LifetimeMinutes"] = "15"
            }).Build();
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddSecurityComponents(configuration);
            services.AddJwtAuthentication();

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        private static SigningCredentials CreateCredentials(string key, string algorithm)
        {
            return new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm);
        }

        private static DefaultHttpContext CreateContext(IServiceProvider provider, string? token)
        {
            DefaultHttpContext context = new DefaultHttpContext { RequestServices = provider };
            if (token != null)
            {
                context.Request.Headers.Authorization = $"Bearer {token}";
            }

            return context;
        }
    }
}
