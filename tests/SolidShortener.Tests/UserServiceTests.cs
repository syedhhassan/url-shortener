using Moq;
using SolidShortener.Application.Interfaces.Repositories;
using SolidShortener.Application.Interfaces.Services;
using SolidShortener.Application.Users.Commands;
using SolidShortener.Application.Users.DTOs;
using SolidShortener.Application.Users.Queries;
using SolidShortener.Application.Users.Services.Implementations;
using SolidShortener.Domain.Entities;
using SolidShortener.Domain.Entities.Users;

namespace SolidShortener.Tests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _repoMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<ITokenGenerator> _tokenMock = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_repoMock.Object, _hasherMock.Object, _tokenMock.Object);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var user = new User("Syed", "syed@example.com", "hashed");
        _repoMock.Setup(r => r.GetUserByEmailAsync("syed@example.com")).ReturnsAsync(user);
        _hasherMock.Setup(h => h.VerifyPassword("plaintext", "hashed")).Returns(true);
        _tokenMock.Setup(t => t.GenerateToken(It.IsAny<UserDTO>())).Returns(("jwt-token", DateTime.UtcNow.AddMinutes(60)));

        // Act
        var result = await _sut.AuthenticateAsync(new AuthenticateUserQuery 
            { Email = "syed@example.com", Password = "plaintext" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("jwt-token", result.Token);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_ReturnsNull()
    {
        var user = new User("Syed", "syed@example.com", "hashed");
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _hasherMock.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await _sut.AuthenticateAsync(new AuthenticateUserQuery 
            { Email = "syed@example.com", Password = "wrong" });

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownEmail_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var result = await _sut.AuthenticateAsync(new AuthenticateUserQuery 
            { Email = "ghost@example.com", Password = "any" });

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateEmail_ThrowsConflict()
    {
        // This test will FAIL until you add the duplicate check — that's intentional.
        // Write the test first, then make it pass.
        var existing = new User("Someone", "taken@example.com", "hash");
        _repoMock.Setup(r => r.GetUserByEmailAsync("taken@example.com")).ReturnsAsync(existing);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterUserAsync(new RegisterUserCommand 
                { Name = "Syed", Email = "taken@example.com", Password = "pass" }));
    }
}