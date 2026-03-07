using Moq;
using FluentAssertions;
using OneDriveAccessGuard.Core.Interfaces;
using OneDriveAccessGuard.Core.Models;
using OneDriveAccessGuard.Core.Enums;
using OneDriveAccessGuard.UI.ViewModels;

namespace OneDriveAccessGuard.Tests;

public class SharedItemsViewModelTests
{
    private readonly Mock<IGraphService> _graphMock = new();
    private readonly Mock<ISharedItemRepository> _repoMock = new();
    private readonly Mock<IAuditLogRepository> _auditMock = new();
    private readonly Mock<IAuthService> _authMock = new();

    [Fact]
    public async Task LoadAsync_ShouldPopulateDisplayItems()
    {
        // Arrange
        var items = new List<SharedItem>
        {
            new() { Id = "1", Name = "secret.xlsx", RiskLevel = RiskLevel.High },
            new() { Id = "2", Name = "public.docx", RiskLevel = RiskLevel.Low },
        };
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(items);

        var vm = new SharedItemsViewModel(
            _graphMock.Object, _repoMock.Object,
            _auditMock.Object, _authMock.Object);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.DisplayItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task FilterByRiskLevel_ShouldReturnOnlyHighRisk()
    {
        // Arrange
        var items = new List<SharedItem>
        {
            new() { Id = "1", Name = "high.xlsx",   RiskLevel = RiskLevel.High },
            new() { Id = "2", Name = "medium.docx", RiskLevel = RiskLevel.Medium },
            new() { Id = "3", Name = "safe.txt",    RiskLevel = RiskLevel.Safe },
        };
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(items);

        var vm = new SharedItemsViewModel(
            _graphMock.Object, _repoMock.Object,
            _auditMock.Object, _authMock.Object);

        await vm.LoadAsync();

        // Act
        vm.FilterRiskLevel = RiskLevel.High;

        // Assert
        vm.DisplayItems.Should().HaveCount(1);
        vm.DisplayItems[0].Name.Should().Be("high.xlsx");
    }
}
