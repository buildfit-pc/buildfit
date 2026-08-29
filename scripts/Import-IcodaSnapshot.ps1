[CmdletBinding()]
param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot '..\src\BuildFit.Web\wwwroot\data\source-manifest.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\icoda-options.snapshot.json')
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$records = [System.Collections.Generic.List[object]]::new()
$optionPattern = '(?s)<li class="op_li\s+[^"]*"\s+default_yn="Y"\s+multi_yn="(?<multi>[YN])"\s+group_name="(?<group>[^"]+)".*?opname="(?<name>[^"]+)".*?</li>'

foreach ($source in $manifest.sources) {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $source.url -TimeoutSec 30
    if ($response.StatusCode -ne 200) {
        throw "아이코다 응답 실패: $($source.url) [$($response.StatusCode)]"
    }

    $matches = [regex]::Matches($response.Content, $optionPattern)
    if ($matches.Count -eq 0) {
        throw "옵션을 찾지 못했습니다: $($source.url)"
    }

    foreach ($match in $matches) {
        $block = $match.Value
        $itemMatch = [regex]::Match($block, "view_detail\(.*?,'(?<id>\d+)'\)")
        $priceMatch = [regex]::Match($block, 'price="(?<price>\d+)"\s+val="(?<quantity>\d+)"')
        if (-not $itemMatch.Success -or -not $priceMatch.Success) {
            continue
        }

        $records.Add([pscustomobject]@{
            sourceId = $source.id
            sourceUrl = $source.url
            capturedAt = [DateTimeOffset]::Now.ToString('o')
            group = [System.Net.WebUtility]::HtmlDecode($match.Groups['group'].Value)
            itemId = $itemMatch.Groups['id'].Value
            name = [System.Net.WebUtility]::HtmlDecode($match.Groups['name'].Value)
            amountKrw = [int]$priceMatch.Groups['price'].Value
            quantity = [int]$priceMatch.Groups['quantity'].Value
        })
    }
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
$snapshot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "아이코다 옵션 $($snapshot.records.Count)건을 $OutputPath 에 저장했습니다."
