namespace BuildFit.Core;

public static class CatalogValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(BuildFitData data)
    {
        var issues = new List<ValidationIssue>();
        var products = data.Products.Products;
        var productIds = products.Select(product => product.Id).ToHashSet(StringComparer.Ordinal);
        var sourceIds = data.Sources.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);

        AddDuplicates(products.Select(product => product.Id), "PRODUCT_ID_DUPLICATE", "중복 부품 ID", issues);
        AddDuplicates(data.Builds.Builds.Select(build => build.Id), "BUILD_ID_DUPLICATE", "중복 조합 ID", issues);
        AddDuplicates(data.Sources.Sources.Select(source => source.Id), "SOURCE_ID_DUPLICATE", "중복 출처 ID", issues);

        foreach (var product in products)
        {
            if (product.Price.AmountKrw <= 0)
            {
                issues.Add(new("PRICE_INVALID", $"{product.Name}: 가격은 0원보다 커야 합니다.", true));
            }

            if (!sourceIds.Contains(product.SourceId))
            {
                issues.Add(new("SOURCE_NOT_FOUND", $"{product.Name}: 출처 {product.SourceId}가 없습니다.", true));
            }

            if (string.IsNullOrWhiteSpace(product.SourceProductId) || !product.SourceProductId.All(char.IsAsciiDigit))
            {
                issues.Add(new("SOURCE_PRODUCT_ID_INVALID", $"{product.Name}: 아이코다 상품 ID가 올바르지 않습니다.", true));
            }

            ValidateRequiredSpec(product, issues);
        }

        foreach (var build in data.Builds.Builds)
        {
            foreach (var component in build.Components)
            {
                if (component.Quantity <= 0)
                {
                    issues.Add(new("QUANTITY_INVALID", $"{build.Name}: {component.ProductId} 수량이 올바르지 않습니다.", true));
                }

                if (!productIds.Contains(component.ProductId))
                {
                    issues.Add(new("PRODUCT_NOT_FOUND", $"{build.Name}: 부품 {component.ProductId}가 없습니다.", true));
                }
            }
        }

        return issues;
    }

    private static void AddDuplicates(
        IEnumerable<string> values,
        string code,
        string label,
        ICollection<ValidationIssue> issues)
    {
        foreach (var duplicate in values.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            issues.Add(new(code, $"{label}: {duplicate.Key}", true));
        }
    }

    private static void ValidateRequiredSpec(Product product, ICollection<ValidationIssue> issues)
    {
        var valid = product.Category switch
        {
            ProductCategory.Cpu => Has(product.Spec.Socket) && Has(product.Spec.MemoryType)
                && product.Spec.PeakPowerWatts > 0 && product.Spec.IntegratedGraphics is not null,
            ProductCategory.CpuCooler => product.Spec.HeightMm > 0
                && product.Spec.CoolingCapacityWatts > 0 && product.Spec.SupportedSockets.Count > 0,
            ProductCategory.Motherboard => Has(product.Spec.Socket) && Has(product.Spec.MemoryType)
                && Has(product.Spec.FormFactor) && product.Spec.MemorySlots > 0 && product.Spec.M2Slots > 0,
            ProductCategory.Memory => Has(product.Spec.MemoryType) && product.Spec.MemoryCapacityGb > 0,
            ProductCategory.GraphicsCard => product.Spec.LengthMm > 0 && product.Spec.PeakPowerWatts > 0
                && product.Spec.RequiredPcie8PinCount is not null && product.Spec.RequiredPcie12V2X6Count is not null,
            ProductCategory.Storage => product.Spec.StorageCapacityGb > 0 && Has(product.Spec.FormFactor),
            ProductCategory.Case => product.Spec.MaxGpuLengthMm > 0 && product.Spec.MaxCoolerHeightMm > 0
                && product.Spec.SupportedMotherboardFormFactors.Count > 0,
            ProductCategory.PowerSupply => product.Spec.RatedWatts > 0 && Has(product.Spec.FormFactor)
                && product.Spec.Pcie8PinCount is not null && product.Spec.Pcie12V2X6Count is not null,
            ProductCategory.Assembly => true,
            _ => false
        };

        if (!valid)
        {
            issues.Add(new("SPEC_REQUIRED", $"{product.Name}: {product.Category} 필수 제원이 누락되었습니다.", true));
        }
    }

    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);
}
