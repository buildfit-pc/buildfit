using System.Text.Json.Serialization;

namespace BuildFit.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ProductCategory>))]
public enum ProductCategory
{
    Cpu,
    CpuCooler,
    Motherboard,
    Memory,
    GraphicsCard,
    Storage,
    Case,
    PowerSupply,
    Assembly
}

[JsonConverter(typeof(JsonStringEnumConverter<BuildPurpose>))]
public enum BuildPurpose
{
    Office,
    Gaming,
    Creator
}

[JsonConverter(typeof(JsonStringEnumConverter<PreferenceMode>))]
public enum PreferenceMode
{
    ImmediatePerformance,
    Balanced,
    ExpansionFirst
}

public sealed record PriceSnapshot(
    int AmountKrw,
    int ShippingKrw,
    DateTimeOffset CapturedAt,
    bool Available);

public sealed record ProductSpec
{
    public string? Socket { get; init; }
    public string? MemoryType { get; init; }
    public int? MemoryCapacityGb { get; init; }
    public int? MemorySlots { get; init; }
    public int? MaxMemoryGb { get; init; }
    public string? FormFactor { get; init; }
    public IReadOnlyList<string> SupportedMotherboardFormFactors { get; init; } = [];
    public IReadOnlyList<string> SupportedSockets { get; init; } = [];
    public int? LengthMm { get; init; }
    public int? HeightMm { get; init; }
    public int? MaxGpuLengthMm { get; init; }
    public int? MaxCoolerHeightMm { get; init; }
    public int? PeakPowerWatts { get; init; }
    public int? CoolingCapacityWatts { get; init; }
    public int? RatedWatts { get; init; }
    public int? Pcie8PinCount { get; init; }
    public int? Pcie12V2X6Count { get; init; }
    public int? RequiredPcie8PinCount { get; init; }
    public int? RequiredPcie12V2X6Count { get; init; }
    public int? M2Slots { get; init; }
    public int? StorageCapacityGb { get; init; }
    public bool? IntegratedGraphics { get; init; }
}

public sealed record Product(
    string Id,
    string Name,
    ProductCategory Category,
    string Manufacturer,
    PriceSnapshot Price,
    string SourceId,
    string SourceProductId,
    ProductSpec Spec);

public sealed record ProductCatalog(
    string SchemaVersion,
    DateTimeOffset CapturedAt,
    IReadOnlyList<Product> Products);

public sealed record BuildComponent(string ProductId, int Quantity);

public sealed record BuildDefinition(
    string Id,
    string Name,
    string Summary,
    IReadOnlyList<BuildPurpose> Purposes,
    PreferenceMode Preference,
    int TargetMemoryGb,
    string TargetResolution,
    int PerformanceScore,
    int QuietScore,
    int ExpansionScore,
    IReadOnlyList<BuildComponent> Components,
    IReadOnlyList<string> Reasons);

public sealed record BuildCatalog(string SchemaVersion, IReadOnlyList<BuildDefinition> Builds);

public sealed record RecommendationProfile(
    string Id,
    PreferenceMode Mode,
    int PerformanceWeight,
    int QuietWeight,
    int ExpansionWeight,
    string MemoryLayoutRule,
    string Description);

public sealed record RecommendationProfileCatalog(
    string SchemaVersion,
    IReadOnlyList<RecommendationProfile> Profiles);

public sealed record CompatibilityRules(
    string SchemaVersion,
    double PowerHeadroomRatio,
    int SystemOverheadWatts,
    int MinimumPowerSupplyWatts,
    int PowerSupplyRoundUpWatts,
    IReadOnlyList<ProductCategory> RequiredCategories);

public sealed record SourceEntry(
    string Id,
    string Provider,
    Uri Url,
    DateTimeOffset CapturedAt,
    string Evidence,
    IReadOnlyList<string> Fields);

public sealed record SourceManifest(string SchemaVersion, IReadOnlyList<SourceEntry> Sources);

public sealed record BuildFitData(
    ProductCatalog Products,
    BuildCatalog Builds,
    CompatibilityRules Rules,
    RecommendationProfileCatalog Profiles,
    SourceManifest Sources);

public sealed record UserPreference(
    BuildPurpose Purpose,
    PreferenceMode Mode,
    int BudgetKrw,
    int MemoryGb,
    string TargetResolution,
    bool PreferQuiet);

public sealed record ValidationIssue(string Code, string Message, bool IsBlocking);

public sealed record CompatibilityResult(
    bool IsCompatible,
    int RequiredPowerSupplyWatts,
    int TotalPriceKrw,
    IReadOnlyList<ValidationIssue> Issues);

public sealed record RecommendedBuild(
    BuildDefinition Definition,
    CompatibilityResult Compatibility,
    int MatchScore,
    IReadOnlyList<Product> Products);
