using System.Text.Json;
using BuildFit.Core;

namespace BuildFit.Core.Tests;

public sealed class DataContractTests
{
    private static readonly string DataDirectory = Path.Combine(FindRepositoryRoot(), "src", "BuildFit.Web", "wwwroot", "data");

    [Fact]
    public void Structured_data_deserializes_without_unknown_fields()
    {
        var data = LoadData();

        Assert.NotEmpty(data.Products.Products);
        Assert.NotEmpty(data.Builds.Builds);
        Assert.Equal(3, data.Profiles.Profiles.Count);
        Assert.NotEmpty(data.Sources.Sources);
    }

    [Fact]
    public void Structured_data_has_no_blocking_contract_issues()
    {
        var issues = CatalogValidator.Validate(LoadData());

        Assert.DoesNotContain(issues, issue => issue.IsBlocking);
    }

    [Fact]
    public void Every_declared_build_is_compatible_and_complete()
    {
        var data = LoadData();
        var products = data.Products.Products.ToDictionary(product => product.Id, StringComparer.Ordinal);

        foreach (var build in data.Builds.Builds)
        {
            var result = CompatibilityEngine.Evaluate(build, products, data.Rules);
            Assert.True(result.IsCompatible, $"{build.Id}: {string.Join(" | ", result.Issues.Select(issue => issue.Message))}");
            Assert.True(result.TotalPriceKrw > 0);
            Assert.True(result.RequiredPowerSupplyWatts >= data.Rules.MinimumPowerSupplyWatts);
        }
    }

    [Fact]
    public void Expansion_profile_prefers_one_32gb_module()
    {
        var data = LoadData();
        var preference = new UserPreference(BuildPurpose.Gaming, PreferenceMode.ExpansionFirst, 3_000_000, 32, "FHD", false);

        var recommendation = RecommendationEngine.Recommend(data, preference).First();

        Assert.Equal("fhd-expansion-32", recommendation.Definition.Id);
        var memory = recommendation.Definition.Components.Single(component => component.ProductId.StartsWith("memory-", StringComparison.Ordinal));
        Assert.Equal("memory-klevv-ddr5-32", memory.ProductId);
        Assert.Equal(1, memory.Quantity);
    }

    [Fact]
    public void Performance_profile_prefers_two_16gb_modules()
    {
        var data = LoadData();
        var preference = new UserPreference(BuildPurpose.Gaming, PreferenceMode.ImmediatePerformance, 3_000_000, 32, "FHD", false);

        var recommendation = RecommendationEngine.Recommend(data, preference).First();

        Assert.Equal("fhd-performance-32", recommendation.Definition.Id);
        var memory = recommendation.Definition.Components.Single(component => component.ProductId.StartsWith("memory-", StringComparison.Ordinal));
        Assert.Equal("memory-micron-ddr5-16", memory.ProductId);
        Assert.Equal(2, memory.Quantity);
    }

    [Fact]
    public void Resolution_is_a_hard_recommendation_constraint()
    {
        var data = LoadData();
        var preference = new UserPreference(BuildPurpose.Gaming, PreferenceMode.ImmediatePerformance, 4_000_000, 32, "QHD", false);

        var recommendations = RecommendationEngine.Recommend(data, preference);

        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, item => Assert.Equal("QHD", item.Definition.TargetResolution));
    }

    [Fact]
    public void Product_prices_are_traceable_to_the_icoda_snapshot()
    {
        var data = LoadData();
        var snapshotPath = Path.Combine(FindRepositoryRoot(), "artifacts", "icoda-options.snapshot.json");
        var snapshot = JsonSerializer.Deserialize<IcodaSnapshot>(File.ReadAllText(snapshotPath), JsonDefaults.Options)
            ?? throw new InvalidDataException($"스냅샷을 읽지 못했습니다: {snapshotPath}");

        foreach (var product in data.Products.Products)
        {
            Assert.Contains(snapshot.Records, record =>
                record.SourceId == product.SourceId
                && record.ItemId == product.SourceProductId
                && record.AmountKrw == product.Price.AmountKrw);
        }
    }

    private static BuildFitData LoadData() => new(
        Read<ProductCatalog>("products.json"),
        Read<BuildCatalog>("builds.json"),
        Read<CompatibilityRules>("compatibility-rules.json"),
        Read<RecommendationProfileCatalog>("recommendation-profiles.json"),
        Read<SourceManifest>("source-manifest.json"));

    private static T Read<T>(string fileName)
    {
        var path = Path.Combine(DataDirectory, fileName);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options)
            ?? throw new InvalidDataException($"JSON을 읽지 못했습니다: {path}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BuildFit.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("BuildFit 저장소 루트를 찾지 못했습니다.");
    }

    private sealed record IcodaSnapshot(string SchemaVersion, DateTimeOffset GeneratedAt, IReadOnlyList<IcodaRecord> Records);
    private sealed record IcodaRecord(string SourceId, string SourceUrl, DateTimeOffset CapturedAt, string Group, string ItemId, string Name, int AmountKrw, int Quantity);
}
