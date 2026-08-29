using System.Text.Json;
using BuildFit.Core;

namespace BuildFit.Web.Services;

public sealed class BuildFitDataService(HttpClient httpClient)
{
    public async Task<BuildFitData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var products = await LoadAsync<ProductCatalog>("data/products.json", cancellationToken);
        var builds = await LoadAsync<BuildCatalog>("data/builds.json", cancellationToken);
        var rules = await LoadAsync<CompatibilityRules>("data/compatibility-rules.json", cancellationToken);
        var profiles = await LoadAsync<RecommendationProfileCatalog>("data/recommendation-profiles.json", cancellationToken);
        var sources = await LoadAsync<SourceManifest>("data/source-manifest.json", cancellationToken);

        var data = new BuildFitData(products, builds, rules, profiles, sources);
        var blockingIssues = CatalogValidator.Validate(data).Where(issue => issue.IsBlocking).ToArray();
        if (blockingIssues.Length > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, blockingIssues.Select(issue => $"[{issue.Code}] {issue.Message}")));
        }

        return data;
    }

    private async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = await httpClient.GetStreamAsync(path, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options, cancellationToken)
            ?? throw new InvalidDataException($"{path} 파일이 비어 있습니다.");
    }
}
