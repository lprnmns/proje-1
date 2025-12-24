param(
    [string]$Output = "data/okx_futures_symbols.json"
)

$url = "https://www.okx.com/api/v5/public/instruments?instType=SWAP"

try {
    $resp = Invoke-RestMethod -Uri $url -Method Get
} catch {
    Write-Error "OKX API çağrısı başarısız: $($_.Exception.Message)"
    exit 1
}

if ($resp.code -ne "0") {
    Write-Error "OKX API hata döndürdü: $($resp.code) - $($resp.msg)"
    exit 1
}

$symbols = $resp.data |
    ForEach-Object { ($_.instId -split "-")[0].ToUpperInvariant() } |
    Sort-Object -Unique

$dir = Split-Path -Parent $Output
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$symbols | ConvertTo-Json -Depth 1 | Set-Content -Encoding UTF8 -Path $Output
Write-Host "OKX futures sembolleri kaydedildi: $Output"
Write-Host "Toplam sembol: $($symbols.Count)"
