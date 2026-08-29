[CmdletBinding()]
param(
    [string]$ManifestPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Join-Path $PSScriptRoot '..'
if (-not $ManifestPath) { $ManifestPath = Join-Path $projectRoot 'src\BuildFit.Web\wwwroot\data\source-manifest.json' }
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot 'artifacts\icoda-options.snapshot.json' }
$manifest = Get-Content -Raw -Encoding utf8 -LiteralPath $ManifestPath | ConvertFrom-Json
$records = [System.Collections.Generic.List[object]]::new()
$optionPattern = '(?s)<li class="op_li\s+[^"]*"\s+default_yn="Y"\s+multi_yn="(?<multi>[YN])"\s+group_name="(?<group>[^"]+)".*?opname="(?<name>[^"]+)".*?</li>'

$collectorJob = {
    param($Source, $Pattern)

    $response = Invoke-WebRequest -UseBasicParsing -Uri $Source.url -TimeoutSec 30
    if ($response.StatusCode -ne 200) { throw "ICODA response failed: $($Source.url) [$($response.StatusCode)]" }

    $matches = [regex]::Matches($response.Content, $Pattern)
    if ($matches.Count -eq 0) { throw "No options found: $($Source.url)" }

    foreach ($match in $matches) {
        $block = $match.Value
        $itemMatch = [regex]::Match($block, "view_detail\(.*?,'(?<id>\d+)'\)")
        $priceMatch = [regex]::Match($block, 'price="(?<price>\d+)"\s+val="(?<quantity>\d+)"')
        if (-not $itemMatch.Success -or -not $priceMatch.Success) { continue }

        [pscustomobject]@{
            sourceId = $Source.id
            sourceUrl = $Source.url
            capturedAt = [DateTimeOffset]::Now.ToString('o')
            group = [System.Net.WebUtility]::HtmlDecode($match.Groups['group'].Value)
            itemId = $itemMatch.Groups['id'].Value
            name = [System.Net.WebUtility]::HtmlDecode($match.Groups['name'].Value)
            amountKrw = [int]$priceMatch.Groups['price'].Value
            quantity = [int]$priceMatch.Groups['quantity'].Value
        }
    }
}

# Fetch independent catalog pages concurrently in local runspaces; the request count is unchanged.
$runspacePool = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspacePool(1, [Math]::Min($manifest.sources.Count, 6))
$runspacePool.Open()
$workers = @()
try {
    foreach ($source in $manifest.sources) {
        $worker = [PowerShell]::Create()
        $worker.RunspacePool = $runspacePool
        [void]$worker.AddScript($collectorJob).AddArgument($source).AddArgument($optionPattern)
        $workers += [pscustomobject]@{ PowerShell = $worker; Handle = $worker.BeginInvoke() }
    }
    foreach ($worker in $workers) {
        foreach ($record in $worker.PowerShell.EndInvoke($worker.Handle)) { $records.Add($record) }
    }
}
finally {
    foreach ($worker in $workers) { $worker.PowerShell.Dispose() }
    $runspacePool.Close()
    $runspacePool.Dispose()
}

$targetDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $targetDirectory)) {
    New-Item -ItemType Directory -Path $targetDirectory | Out-Null
}

$snapshot = [ordered]@{
    schemaVersion = '1.0.0'
    generatedAt = [DateTimeOffset]::Now.ToString('o')
    records = $records | Sort-Object sourceId, group, itemId -Unique
}
$snapshot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "아이코다 옵션 $($snapshot.records.Count)건을 $OutputPath 에 저장했습니다."
