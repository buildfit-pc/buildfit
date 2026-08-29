namespace BuildFit.Core;

public static class CompatibilityEngine
{
    public static CompatibilityResult Evaluate(
        BuildDefinition build,
        IReadOnlyDictionary<string, Product> catalog,
        CompatibilityRules rules)
    {
        var issues = new List<ValidationIssue>();
        var selected = Expand(build, catalog, issues);
        if (issues.Any(issue => issue.IsBlocking))
        {
            return new(false, 0, 0, issues);
        }

        foreach (var category in rules.RequiredCategories)
        {
            if (selected.All(item => item.Product.Category != category))
            {
                issues.Add(new("REQUIRED_CATEGORY_MISSING", $"필수 부품 {category}가 없습니다.", true));
            }
        }

        var cpu = Single(selected, ProductCategory.Cpu, issues);
        var motherboard = Single(selected, ProductCategory.Motherboard, issues);
        var cooler = Single(selected, ProductCategory.CpuCooler, issues);
        var graphics = Single(selected, ProductCategory.GraphicsCard, issues);
        var pcCase = Single(selected, ProductCategory.Case, issues);
        var power = Single(selected, ProductCategory.PowerSupply, issues);
        var memory = selected.Where(item => item.Product.Category == ProductCategory.Memory).ToArray();
        var storageCount = selected.Where(item => item.Product.Category == ProductCategory.Storage).Sum(item => item.Quantity);

        if (cpu is not null && motherboard is not null)
        {
            RequireEqual(cpu.Spec.Socket, motherboard.Spec.Socket, "CPU_SOCKET_MISMATCH", "CPU와 메인보드 소켓", issues);
            RequireEqual(cpu.Spec.MemoryType, motherboard.Spec.MemoryType, "CPU_MEMORY_MISMATCH", "CPU와 메인보드 메모리 규격", issues);
        }

        if (motherboard is not null)
        {
            foreach (var item in memory)
            {
                RequireEqual(motherboard.Spec.MemoryType, item.Product.Spec.MemoryType, "MEMORY_TYPE_MISMATCH", "메인보드와 메모리 규격", issues);
            }

            var moduleCount = memory.Sum(item => item.Quantity);
            var capacity = memory.Sum(item => (item.Product.Spec.MemoryCapacityGb ?? 0) * item.Quantity);
            if (moduleCount > motherboard.Spec.MemorySlots)
            {
                issues.Add(new("MEMORY_SLOT_OVERFLOW", $"메모리 {moduleCount}개가 슬롯 {motherboard.Spec.MemorySlots}개를 초과합니다.", true));
            }
            if (capacity > motherboard.Spec.MaxMemoryGb)
            {
                issues.Add(new("MEMORY_CAPACITY_OVERFLOW", $"메모리 {capacity}GB가 최대 {motherboard.Spec.MaxMemoryGb}GB를 초과합니다.", true));
            }
            if (storageCount > motherboard.Spec.M2Slots)
            {
                issues.Add(new("M2_SLOT_OVERFLOW", $"M.2 저장장치 {storageCount}개가 슬롯 {motherboard.Spec.M2Slots}개를 초과합니다.", true));
            }
        }

        if (cpu is not null && cooler is not null)
        {
            if (!cooler.Spec.SupportedSockets.Contains(cpu.Spec.Socket, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new("COOLER_SOCKET_MISMATCH", "CPU 쿨러가 CPU 소켓을 지원하지 않습니다.", true));
            }
            if (cooler.Spec.CoolingCapacityWatts < cpu.Spec.PeakPowerWatts)
            {
                issues.Add(new("COOLING_INSUFFICIENT", "CPU 쿨러의 냉각 용량이 CPU 최대 부하보다 낮습니다.", true));
            }
        }

        if (pcCase is not null && motherboard is not null
            && !pcCase.Spec.SupportedMotherboardFormFactors.Contains(motherboard.Spec.FormFactor, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new("CASE_BOARD_MISMATCH", "케이스가 메인보드 폼팩터를 지원하지 않습니다.", true));
        }

        if (pcCase is not null && graphics is not null && graphics.Spec.LengthMm > pcCase.Spec.MaxGpuLengthMm)
        {
            issues.Add(new("GPU_LENGTH_OVERFLOW", "그래픽카드 길이가 케이스 허용 길이를 초과합니다.", true));
        }

        if (pcCase is not null && cooler is not null && cooler.Spec.HeightMm > pcCase.Spec.MaxCoolerHeightMm)
        {
            issues.Add(new("COOLER_HEIGHT_OVERFLOW", "CPU 쿨러 높이가 케이스 허용 높이를 초과합니다.", true));
        }

        if (cpu is not null && graphics is null && cpu.Spec.IntegratedGraphics != true)
        {
            issues.Add(new("GRAPHICS_REQUIRED", "내장 그래픽이 없는 CPU에는 그래픽카드가 필요합니다.", true));
        }

        var loadWatts = (cpu?.Spec.PeakPowerWatts ?? 0) + (graphics?.Spec.PeakPowerWatts ?? 0) + rules.SystemOverheadWatts;
        var requiredWatts = RoundUp(
            Math.Max(rules.MinimumPowerSupplyWatts, (int)Math.Ceiling(loadWatts * rules.PowerHeadroomRatio)),
            rules.PowerSupplyRoundUpWatts);

        if (power is not null)
        {
            if (power.Spec.RatedWatts < requiredWatts)
            {
                issues.Add(new("PSU_CAPACITY_INSUFFICIENT", $"권장 {requiredWatts}W보다 낮은 {power.Spec.RatedWatts}W 파워입니다.", true));
            }
            if ((power.Spec.Pcie8PinCount ?? 0) < (graphics?.Spec.RequiredPcie8PinCount ?? 0))
            {
                issues.Add(new("PSU_PCIE8_INSUFFICIENT", "파워의 PCIe 8핀 커넥터가 부족합니다.", true));
            }
            if ((power.Spec.Pcie12V2X6Count ?? 0) < (graphics?.Spec.RequiredPcie12V2X6Count ?? 0))
            {
                issues.Add(new("PSU_12V2X6_INSUFFICIENT", "파워의 12V-2x6 커넥터가 부족합니다.", true));
            }
        }

        var total = selected.Sum(item => (item.Product.Price.AmountKrw + item.Product.Price.ShippingKrw) * item.Quantity);
        return new(!issues.Any(issue => issue.IsBlocking), requiredWatts, total, issues);
    }

    private static IReadOnlyList<(Product Product, int Quantity)> Expand(
        BuildDefinition build,
        IReadOnlyDictionary<string, Product> catalog,
        ICollection<ValidationIssue> issues)
    {
        var selected = new List<(Product, int)>();
        foreach (var component in build.Components)
        {
            if (!catalog.TryGetValue(component.ProductId, out var product))
            {
                issues.Add(new("PRODUCT_NOT_FOUND", $"부품 {component.ProductId}가 없습니다.", true));
                continue;
            }
            selected.Add((product, component.Quantity));
        }
        return selected;
    }

    private static Product? Single(
        IReadOnlyList<(Product Product, int Quantity)> selected,
        ProductCategory category,
        ICollection<ValidationIssue> issues)
    {
        var matches = selected.Where(item => item.Product.Category == category).ToArray();
        if (matches.Length > 1)
        {
            issues.Add(new("CATEGORY_DUPLICATE", $"{category} 부품이 여러 종류 선택되었습니다.", true));
        }
        return matches.FirstOrDefault().Product;
    }

    private static void RequireEqual(
        string? left,
        string? right,
        string code,
        string label,
        ICollection<ValidationIssue> issues)
    {
        if (!string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(code, $"{label}이 일치하지 않습니다: {left} / {right}", true));
        }
    }

    private static int RoundUp(int value, int unit) => (int)Math.Ceiling(value / (double)unit) * unit;
}
