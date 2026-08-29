namespace BuildFit.Core;

public static class RecommendationEngine
{
    public static IReadOnlyList<RecommendedBuild> Recommend(BuildFitData data, UserPreference preference)
    {
        var products = data.Products.Products.ToDictionary(product => product.Id, StringComparer.Ordinal);
        var profile = data.Profiles.Profiles.Single(item => item.Mode == preference.Mode);

        return data.Builds.Builds
            .Where(build => build.Purposes.Contains(preference.Purpose))
            .Where(build => build.TargetMemoryGb == preference.MemoryGb)
            .Where(build => string.Equals(build.TargetResolution, preference.TargetResolution, StringComparison.OrdinalIgnoreCase))
            .Select(build => CreateRecommendation(build, products, data.Rules, preference, profile))
            .Where(result => result.Compatibility.IsCompatible)
            .OrderByDescending(result => result.MatchScore)
            .ThenBy(result => result.Compatibility.TotalPriceKrw)
            .ToArray();
    }

    private static RecommendedBuild CreateRecommendation(
        BuildDefinition build,
        IReadOnlyDictionary<string, Product> catalog,
        CompatibilityRules rules,
        UserPreference preference,
        RecommendationProfile profile)
    {
        var compatibility = CompatibilityEngine.Evaluate(build, catalog, rules);
        var score = build.PerformanceScore * profile.PerformanceWeight
            + build.QuietScore * profile.QuietWeight
            + build.ExpansionScore * profile.ExpansionWeight;

        if (build.Preference == preference.Mode)
        {
            score += 1_000;
        }
        if (string.Equals(build.TargetResolution, preference.TargetResolution, StringComparison.OrdinalIgnoreCase))
        {
            score += 400;
        }
        if (preference.PreferQuiet)
        {
            score += build.QuietScore * 5;
        }
        if (compatibility.TotalPriceKrw <= preference.BudgetKrw)
        {
            score += 800;
        }
        else
        {
            score -= (compatibility.TotalPriceKrw - preference.BudgetKrw) / 1_000;
        }

        var selectedProducts = build.Components.Select(component => catalog[component.ProductId]).ToArray();
        return new(build, compatibility, score, selectedProducts);
    }
}
