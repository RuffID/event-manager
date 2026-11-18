using EventManager.Api.Controllers;
using EventManager.Api.Tests.Services;
using EventManager.Application.Abstractions.Security;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Services;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace EventManager.Api.Tests.Controllers;

public class AuthFlowTests
{
    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.Admin)]
    public async Task RegisterAndLogin_StorePasswordHashAndIssueTokenWithSavedIdentity(UserRole role)
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        using IServiceScope scope = database.ServiceProvider.CreateScope();
        AuthController controller = new AuthController(scope.ServiceProvider.GetRequiredService<IUserService>());
        RegisterUserDto input = new RegisterUserDto { Login = "student", Password = "private-password" };
        if (role == UserRole.Admin)
        {
            input.Role = UserRole.Admin;
        }

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        IActionResult registered = await controller.Register(input, cancellationToken);
        IActionResult loggedIn = await controller.Login(
            new LoginDto { Login = input.Login, Password = input.Password }, cancellationToken);

        // Assert
        Assert.IsType<NoContentResult>(registered);
        TokenDto response = Assert.IsType<TokenDto>(Assert.IsType<OkObjectResult>(loggedIn).Value);
        User stored = await database.Context.Users.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(role, stored.Role);
        Assert.NotEqual(input.Password, stored.PasswordHash);
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.True(hasher.Verify(input.Password, stored.PasswordHash));
        JsonWebToken token = new JsonWebTokenHandler().ReadJsonWebToken(response.Token);
        Assert.Equal(stored.Id.ToString("D"), token.Subject);
        Assert.Equal(stored.Login, token.GetClaim(JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal(role.ToString(), token.GetClaim("role").Value);
        Assert.DoesNotContain(token.Claims, claim => claim.Value == stored.PasswordHash);
        Assert.DoesNotContain(token.Claims, claim => claim.Value == input.Password);
    }

    [Fact]
    public async Task Login_UnknownUserAndWrongPasswordReturnSameBadRequest()
    {
        // Arrange
        using ServiceTestContext database = new ServiceTestContext();
        using IServiceScope scope = database.ServiceProvider.CreateScope();
        AuthController controller = new AuthController(scope.ServiceProvider.GetRequiredService<IUserService>());
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await controller.Register(new RegisterUserDto { Login = "student", Password = "private-password" }, cancellationToken);

        // Act
        IActionResult missing = await controller.Login(
            new LoginDto { Login = "unknown", Password = "wrong-password" }, cancellationToken);
        IActionResult wrong = await controller.Login(
            new LoginDto { Login = "student", Password = "wrong-password" }, cancellationToken);

        // Assert
        ObjectResult first = Assert.IsType<ObjectResult>(missing);
        ObjectResult second = Assert.IsType<ObjectResult>(wrong);
        Assert.Equal(StatusCodes.Status400BadRequest, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        ProblemDetails firstError = Assert.IsType<ProblemDetails>(first.Value);
        ProblemDetails secondError = Assert.IsType<ProblemDetails>(second.Value);
        Assert.Equal(firstError.Detail, secondError.Detail);
        Assert.DoesNotContain("student", firstError.Detail!);
        Assert.DoesNotContain("wrong-password", firstError.Detail!);
        Assert.Equal(1, await database.Context.Users.CountAsync(cancellationToken));
    }
}
