using Moq;
using SolidShortener.Application.Interfaces.Repositories;
using SolidShortener.Application.Interfaces.Services;
using SolidShortener.Application.Urls.Commands;
using SolidShortener.Application.Urls.Queries;
using SolidShortener.Application.Urls.Services.Implementations;
using SolidShortener.Domain.Entities.Urls;

namespace SolidShortener.Tests;

public class UrlServiceTests
{
    private readonly Mock<IUrlRepository> _repoMock = new();
    private readonly Mock<IShortCodeGenerator> _generatorMock = new();
    private readonly UrlService _sut;

    public UrlServiceTests()
    {
        _sut = new UrlService(_repoMock.Object, _generatorMock.Object);
    }

    [Fact]
    public async Task ShortenUrlAsync_NewUrl_CreatesAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ShortenUrlCommand { UserId = userId, LongUrl = "https://example.com" };

        _repoMock.Setup(r => r.GetByUserAndLongUrlAsync(It.IsAny<GetUrlByUserAndLongUrlQuery>()))
                 .ReturnsAsync((Url?)null);
        _generatorMock.Setup(g => g.Generate(It.IsAny<long>())).Returns("abc123");

        // Act
        var result = await _sut.ShortenUrlAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Url>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Url>()), Times.Once);
    }

    [Fact]
    public async Task ShortenUrlAsync_ExistingDeletedUrl_UndeletesAndReturns()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existing = new Url(userId, "https://example.com", "abc123");
        existing.MarkAsDeleted();

        _repoMock.Setup(r => r.GetByUserAndLongUrlAsync(It.IsAny<GetUrlByUserAndLongUrlQuery>()))
                 .ReturnsAsync(existing);

        var command = new ShortenUrlCommand { UserId = userId, LongUrl = "https://example.com" };

        // Act
        var result = await _sut.ShortenUrlAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.False(existing.IsDeleted);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Url>()), Times.Never);
        _repoMock.Verify(r => r.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task GetUrlByShortCodeAsync_ExpiredUrl_ReturnsNull()
    {
        // Arrange
        var url = new Url(Guid.NewGuid(), "https://example.com", "abc123", 
                          expiresAt: DateTime.UtcNow.AddDays(-1)); // expired yesterday

        _repoMock.Setup(r => r.GetUrlByShortCodeAsync(It.IsAny<GetUrlByShortCodeQuery>()))
                 .ReturnsAsync(url);

        // Act
        var result = await _sut.GetUrlByShortCodeAsync(new GetUrlByShortCodeQuery { ShortCode = "abc123" });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteUrlAsync_WrongUser_ThrowsUnauthorized()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid(); // different user
        var url = new Url(ownerId, "https://example.com", "abc123");

        _repoMock.Setup(r => r.GetUrlByShortCodeAsync(It.IsAny<GetUrlByShortCodeQuery>()))
                 .ReturnsAsync(url);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.DeleteUrlAsync(new DeleteUrlCommand { UserId = requesterId, ShortCode = "abc123" }));
    }

    [Fact]
    public async Task DeleteUrlAsync_NonExistentCode_ThrowsNotFound()
    {
        _repoMock.Setup(r => r.GetUrlByShortCodeAsync(It.IsAny<GetUrlByShortCodeQuery>()))
                 .ReturnsAsync((Url?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.DeleteUrlAsync(new DeleteUrlCommand { UserId = Guid.NewGuid(), ShortCode = "xyz" }));
    }
}