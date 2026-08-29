[CmdletBinding()]
param(
    [int]$IntervalSeconds = 60,
    [int]$Runs = 10
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$collector = Join-Path $PSScriptRoot 'Import-IcodaSnapshot.ps1'
$manifest = Join-Path $projectRoot 'src\BuildFit.Web\wwwroot\data\source-manifest.json'
$output = Join-Path $projectRoot 'artifacts\icoda-options.snapshot.json'
$log = Join-Path $projectRoot 'artifacts\icoda-collection.log'

for ($run = 1; $run -le $Runs; $run++) {
    $startedAt = [DateTimeOffset]::Now.ToString('o')
    try {
        & $collector -ManifestPath $manifest -OutputPath $output
        "[$startedAt] run $run/$Runs completed" | Add-Content -LiteralPath $log -Encoding utf8
    }
    catch {
        "[$startedAt] run $run/$Runs failed: $($_.Exception.Message)" | Add-Content -LiteralPath $log -Encoding utf8
    }

    if ($run -lt $Runs) { Start-Sleep -Seconds $IntervalSeconds }
}
