using AVEXOS.WindowsIntegration;
using Xunit;

namespace AVEXOS.WindowsIntegration.Tests;

public class AppDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_Returns_Results()
    {
        var service = new AppDiscoveryService();
        var results = await service.DiscoverAsync();

        Assert.NotNull(results);
        // On a real Windows machine, there should be at least some apps
        // In CI or minimal environments, this might be empty
    }

    [Fact]
    public async Task DiscoverAsync_Results_Have_Required_Fields()
    {
        var service = new AppDiscoveryService();
        var results = await service.DiscoverAsync();

        foreach (var app in results)
        {
            Assert.False(string.IsNullOrWhiteSpace(app.Id));
            Assert.False(string.IsNullOrWhiteSpace(app.DisplayName));
            Assert.StartsWith("app-", app.Id);
            Assert.Equal(16, app.Id.Length); // "app-" + 12 hex chars
        }
    }

    [Fact]
    public async Task DiscoverAsync_Results_Are_Limited_To_80()
    {
        var service = new AppDiscoveryService();
        var results = await service.DiscoverAsync();

        Assert.True(results.Count <= 80);
    }

    [Fact]
    public async Task DiscoverAsync_Results_Are_Sorted_By_DisplayName()
    {
        var service = new AppDiscoveryService();
        var results = await service.DiscoverAsync();

        if (results.Count < 2) return; // Need at least 2 to verify sorting

        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(
                string.Compare(results[i - 1].DisplayName, results[i].DisplayName, StringComparison.OrdinalIgnoreCase) <= 0,
                $"Results not sorted: '{results[i - 1].DisplayName}' should come before '{results[i].DisplayName}'");
        }
    }

    [Fact]
    public async Task DiscoverAsync_Respects_Cancellation()
    {
        var service = new AppDiscoveryService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DiscoverAsync(cts.Token));
    }
}
