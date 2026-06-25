# azd postdeploy hook — re-binds api.wcaipredictions.com and smoke-tests the
# new bff revision. Uses ARM REST directly because the az CLI's containerapp
# module TLS-resets intermittently from this machine.
#
# Why not full blue/green: azd's Aspire integration force-rewrites the
# Container App spec to Aspire's defaults (activeRevisionsMode=Single,
# traffic=[latestRevision: true, weight: 100]) on every deploy. Any mode flip
# the predeploy hook performs is reverted before the new revision is created,
# so a true "deploy at 0% traffic" model is impossible without Aspire 13.5+
# IaC customization. The next best thing is a loud post-deploy probe: if the
# new revision is broken, surface the failure immediately and tell the user
# which previous revision to roll back to.
#
# Flow:
#   1. Rebind custom domain via ARM PATCH (Aspire strips it every deploy).
#   2. Probe api.wcaipredictions.com — /matches (200) + /me (401).
#   3. If any probe fails: throw with the previous revision name so the user
#      can manually rebind traffic (or run the rollback snippet at the bottom
#      of this file).

$ErrorActionPreference = 'Stop'

$Hostname    = 'api.wcaipredictions.com'
$AppName     = 'bff'
$ResourceGrp = 'rg-matchforecast-prod'
$EnvName     = 'cae-u3dnj7kwrcz7s'
$Sub         = '890828df-61ca-41e3-a322-e709db504a6c'
$ApiVersion  = '2024-03-01'

$cappUrl = "https://management.azure.com/subscriptions/$Sub/resourceGroups/$ResourceGrp/providers/Microsoft.App/containerApps/$AppName"
$certListUrl = "https://management.azure.com/subscriptions/$Sub/resourceGroups/$ResourceGrp/providers/Microsoft.App/managedEnvironments/$EnvName/managedCertificates"

function Get-Token {
    $tok = az account get-access-token --query accessToken -o tsv
    if (-not $tok) { throw "[postdeploy] Failed to acquire ARM token." }
    return $tok
}

function Invoke-Arm {
    param([string]$Method, [string]$Url, [object]$Body)
    # ARM endpoint TLS-resets intermittently from this machine. Retry with backoff.
    $maxAttempts = 5
    $delaySec = 5
    for ($i = 1; $i -le $maxAttempts; $i++) {
        try {
            $token = Get-Token
            $headers = @{ Authorization = "Bearer $token"; 'Content-Type' = 'application/json' }
            if ($Body) {
                return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers `
                    -Body ($Body | ConvertTo-Json -Depth 20)
            }
            return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers
        } catch {
            if ($i -eq $maxAttempts) { throw }
            Write-Host "[postdeploy] ARM call failed (attempt $i/$maxAttempts): $($_.Exception.Message). Retrying in ${delaySec}s..."
            Start-Sleep -Seconds $delaySec
        }
    }
}

# ----- 1. Rebind custom domain -----------------------------------------------

Write-Host "[postdeploy] Looking up managed certificate for $Hostname..."

$certResp = Invoke-Arm -Method GET -Url "$($certListUrl)?api-version=$ApiVersion"
$cert = $certResp.value | Where-Object {
    $_.properties.subjectName -eq $Hostname -and $_.properties.provisioningState -eq 'Succeeded'
} | Select-Object -First 1

if (-not $cert) {
    throw "[postdeploy] No Succeeded managed cert for $Hostname in $EnvName. Re-issue one via the Portal: Container App Environments -> $EnvName -> Certificates -> Managed."
}

Write-Host "[postdeploy] Using cert: $($cert.name)"
Write-Host "[postdeploy] PATCHing $AppName with customDomains[$Hostname]..."

$patchBody = @{
    properties = @{
        configuration = @{
            ingress = @{
                customDomains = @(
                    @{
                        name = $Hostname
                        bindingType = 'SniEnabled'
                        certificateId = $cert.id
                    }
                )
            }
        }
    }
}

Invoke-Arm -Method PATCH -Url "$($cappUrl)?api-version=$ApiVersion" -Body $patchBody | Out-Null
Write-Host "[postdeploy] Custom domain rebound."

# ----- 2. Smoke probe live URL -----------------------------------------------

Write-Host "[postdeploy] Cooling 15s for cert binding to propagate..."
Start-Sleep -Seconds 15

$probes = @(
    @{ Path = '/matches'; Expect = 200; Why = 'public list endpoint — exercises DB + cache' },
    @{ Path = '/me';      Expect = 401; Why = 'auth-protected — 500 would mean broken Clerk authority/JWKS' }
)

$failures = @()
foreach ($probe in $probes) {
    $url = "https://$Hostname$($probe.Path)"
    try {
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -ErrorAction Stop
        $code = [int]$resp.StatusCode
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    }
    if ($code -eq $probe.Expect) {
        Write-Host "[postdeploy]   $($probe.Path) -> $code [pass]  ($($probe.Why))"
    } else {
        $failures += "$($probe.Path) returned $code (expected $($probe.Expect))"
        Write-Host "[postdeploy]   $($probe.Path) -> $code [FAIL, expected $($probe.Expect)]"
    }
}

# ----- 3. Fail loud with rollback target -------------------------------------

if ($failures.Count -gt 0) {
    $envDir = Split-Path $PSScriptRoot -Parent
    $snapshotPath = Join-Path $envDir '.azure/matchforecast-prod/.previous-bff-revision'
    $rollbackTarget = if (Test-Path $snapshotPath) { (Get-Content $snapshotPath -Raw).Trim() } else { '<unknown — run az containerapp revision list>' }

    Write-Host ""
    Write-Host "[postdeploy] PROBE FAILED. The new revision is reachable but returned the wrong status."
    Write-Host "[postdeploy] Rollback target: $rollbackTarget"
    Write-Host "[postdeploy] Manual rollback:"
    Write-Host "[postdeploy]   az containerapp revision activate -g $ResourceGrp -n $AppName --revision $rollbackTarget"
    Write-Host "[postdeploy]   az containerapp ingress traffic set -g $ResourceGrp -n $AppName --revision-weight `"$rollbackTarget=100`""
    Write-Host ""
    throw "[postdeploy] Smoke probe failed: $($failures -join '; ')."
}

Write-Host "[postdeploy] All probes passed. Deploy verified healthy."
