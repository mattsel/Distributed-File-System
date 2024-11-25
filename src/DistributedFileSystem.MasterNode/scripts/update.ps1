param (
    [string]$address,
    [string]$action
)

Write-Host "Received address: '$address'"
Write-Host "Received action: '$action'"

if (-not $address) {
    Write-Host "Error: No address provided."
    exit 1
}
if ($action -notin @('CreateNode', 'DeleteNode')) {
    Write-Host "Error: Invalid action specified. Use 'CreateNode' or 'DeleteNode'."
    exit 1
}
$configFilePath = "..\Prometheus\prometheus.yml"
if (-not (Test-Path $configFilePath)) {
    Write-Host "Error: Configuration file does not exist at the specified path."
    exit 1
}

function Add-ScrapeConfig {
    param (
        [string]$address
    )
    $scrapeConfig = @"
  - job_name: 'scrape_$address'
    scrape_interval: 15s
    static_configs:
      - targets: ['$address']
    tls_config:
      insecure_skip_verify: true"
"@

    $configContent = Get-Content $configFilePath -Raw
    if ($configContent -match "scrape_$address") {
        Write-Host "Scrape config for address '$address' already exists in the configuration file."
    } else {
        Add-Content -Path $configFilePath -Value $scrapeConfig
        Write-Host "Scrape config for address '$address' has been added to the configuration file."
        Invoke-RestMethod -Method Post -Uri $address/-/reload

    }
}

function Remove-ScrapeConfig {
    param (
        [string]$address
    )
    $configContent = Get-Content $configFilePath -Raw

    $address = $address -replace "^https://", ""

    $scrapeConfig = @"
  - job_name: 'scrape_$address'
    scrape_interval: 15s
    static_configs:
      - targets: ['$address']
    tls_config:
      insecure_skip_verify: true"
"@

    if ($configContent -match [regex]::Escape($scrapeConfig)) {
        $updatedConfig = $configContent -replace [regex]::Escape($scrapeConfig), ""

        Set-Content -Path $configFilePath -Value $updatedConfig
        Write-Host "Scrape config for address '$address' has been removed from the configuration file."
    } else {
        Write-Host "Scrape config for address '$address' does not exist in the configuration file."
    }
}

if ($action -eq 'CreateNode') {
    Add-ScrapeConfig -address $address
} elseif ($action -eq 'DeleteNode') {
    Remove-ScrapeConfig -address $address
}
