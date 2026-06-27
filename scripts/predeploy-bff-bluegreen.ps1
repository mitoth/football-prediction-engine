# azd predeploy hook — snapshots the currently serving bff revision name so
# the postdeploy probe can tell the user which revision to roll back to if the
# new one is broken.
#
# Uses ARM REST directly (Invoke-RestMethod) — the az CLI's containerapp
# revision list intermittently TLS-resets on this machine and would otherwise
# block the entire deploy.

$ErrorActionPreference = 'Stop'

$AppName     = 'bff'
$ResourceGrp = 'rg-matchforecast-prod'
$Sub         = '890828df-61ca-41e3-a322-e709db504a6c'
$ApiVersion  = '2024-03-01'

$revUrl = "https://management.azure.com/subscriptions/$Sub/resourceGroups/$ResourceGrp/providers/Microsoft.App/containerApps/$AppName/revisions?api-version=$ApiVersion"

function Invoke-Arm {
    param([string]$Url)
    $maxAttempts = 5
    $delaySec = 5
    for ($i = 1; $i -le $maxAttempts; $i++) {
        try {
            $token = az account get-access-token --query accessToken -o tsv
            if (-not $token) { throw "[predeploy] Failed to acquire ARM token." }
            return Invoke-RestMethod -Method GET -Uri $Url `
                -Headers @{ Authorization = "Bearer $token" }
        } catch {
            if ($i -eq $maxAttempts) { throw }
            Write-Host "[predeploy] ARM call failed (attempt $i/$maxAttempts): $($_.Exception.Message). Retrying in ${delaySec}s..."
            Start-Sleep -Seconds $delaySec
        }
    }
}

$response = Invoke-Arm -Url $revUrl
$active = $response.value | Where-Object { $_.properties.active -eq $true }
$current = $active | Sort-Object { [DateTime]$_.properties.createdTime } -Descending | Select-Object -First 1

if (-not $current) { throw "[predeploy] No active revision found for $AppName." }
$currentRev = $current.name

$envDir = Split-Path $PSScriptRoot -Parent
$snapshotPath = Join-Path $envDir '.azure/matchforecast-prod/.previous-bff-revision'
# On a clean CI runner the gitignored .azure/<env> dir may not exist yet — the
# env state lives in remote (blob) storage. Ensure the parent dir before write.
$snapshotDir = Split-Path $snapshotPath -Parent
if (-not (Test-Path $snapshotDir)) { New-Item -ItemType Directory -Path $snapshotDir -Force | Out-Null }
$currentRev | Out-File -FilePath $snapshotPath -NoNewline -Encoding ascii

Write-Host "[predeploy] Snapshotted previous revision -> $currentRev"
