using EventManager.Api.Controllers;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EventManager.Api.Tests.Controllers
{
    public class AuthControllerTests
    {
        [Fact]
        public async Task Register_ReturnsCreatedUserAndForwardsInput()
        {
            // Arrange
            UserInfo user = new UserInfo { Id = Guid.NewGuid(), Login = "student", Role = UserRole.Admin };
            UserServiceStub service = new UserServiceStub { RegisterResult = ServiceResult<UserInfo>.Succeed(user) };
            AuthController controller = new AuthController(service);
            RegisterUserDto input = new RegisterUserDto { Login = "student", Password = "password", Role = UserRole.Admin };
            using CancellationTokenSource cancellation = new CancellationTokenSource();

            // Act
            IActionResult result = await controller.Register(input, cancellation.Token);

            // Assert
            ObjectResult response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
            Assert.Same(user, response.Value);
            Assert.Same(input, service.RegisterInput);
            Assert.Equal(cancellation.Token, service.ReceivedToken);
        }

        [Fact]
        public async Task Login_ReturnsTokenAndForwardsInput()
        {
            // Arrange
            TokenDto token = new TokenDto { Token = "issued-token" };
            UserServiceStub service = new UserServiceStub { LoginResult = ServiceResult<TokenDto>.Succeed(token) };
            AuthController controller = new AuthController(service);
            LoginDto input = new LoginDto { Login = "student", Password = "password" };
            using CancellationTokenSource cancellation = new CancellationTokenSource();

            // Act
            IActionResult result = await controller.Login(input, cancellation.Token);

            // Assert
            Assert.Same(token, Assert.IsType<OkObjectResult>(result).Value);
            Assert.Same(input, service.LoginInput);
            Assert.Equal(cancellation.Token, service.ReceivedToken);
        }

        [Theory]
        [InlineData(ServiceErrorType.Validation, StatusCodes.Status400BadRequest)]
        [InlineData(ServiceErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
        [InlineData(ServiceErrorType.Conflict, StatusCodes.Status409Conflict)]
        [InlineData(ServiceErrorType.Internal, StatusCodes.Status500InternalServerError)]
        public async Task AuthActions_MapServiceErrors(ServiceErrorType error, int status)
        {
            // Arrange
            const string MESSAGE = "Operation refused.";
            UserServiceStub service = new UserServiceStub
            {
                RegisterResult = ServiceResult<UserInfo>.Fail(error, MESSAGE),
                LoginResult = ServiceResult<TokenDto>.Fail(error, MESSAGE)
            };
            AuthController controller = new AuthController(service);

            // Act
            IActionResult register = await controller.Register(new RegisterUserDto(), TestContext.Current.CancellationToken);
            IActionResult login = await controller.Login(new LoginDto(), TestContext.Current.CancellationToken);

            // Assert
            Assert.All(new[] { register, login }, result =>
            {
                ObjectResult response = Assert.IsType<ObjectResult>(result);
                Assert.Equal(status, response.StatusCode);
                ProblemDetails problem = Assert.IsType<ProblemDetails>(response.Value);
                Assert.Equal(MESSAGE, problem.Detail);
            });
        }

        private class UserServiceStub : IUserService
        {
            public ServiceResult<UserInfo> RegisterResult { get; set; } = ServiceResult<UserInfo>.Fail(ServiceErrorType.Conflict, "Login exists.");
            public ServiceResult<TokenDto> LoginResult { get; set; } = ServiceResult<TokenDto>.Fail(ServiceErrorType.Unauthorized, "Invalid credentials.");
            public RegisterUserDto? RegisterInput { get; private set; }
            public LoginDto? LoginInput { get; private set; }
            public CancellationToken ReceivedToken { get; private set; }

            public Task<ServiceResult<UserInfo>> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
            {
                RegisterInput = dto;
                ReceivedToken = cancellationToken;
                return Task.FromResult(RegisterResult);
            }

            public Task<ServiceResult<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
            {
                LoginInput = dto;
                ReceivedToken = cancellationToken;
                return Task.FromResult(LoginResult);
            }
        }
    }
}
