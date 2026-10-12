param(
    [Parameter(Mandatory = $true)]
    [string]$DotnetExe,
    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$logDirectory = Split-Path -Parent $LogPath
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Set-Content -LiteralPath $LogPath -Value "Tbot Ultra local CI preflight" -Encoding UTF8

function Invoke-PreflightStep {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    Add-Content -LiteralPath $LogPath -Value "`r`n=== $Name ===" -Encoding UTF8
    $outputPath = Join-Path $logDirectory "preflight-step.stdout.log"
    $errorPath = Join-Path $logDirectory "preflight-step.stderr.log"
    try {
        $process = Start-Process -FilePath $DotnetExe -ArgumentList $Arguments `
            -WorkingDirectory $repoRoot -WindowStyle Hidden -Wait -PassThru `
            -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath
        Get-Content -LiteralPath $outputPath -ErrorAction SilentlyContinue |
            Add-Content -LiteralPath $LogPath -Encoding UTF8
        Get-Content -LiteralPath $errorPath -ErrorAction SilentlyContinue |
            Add-Content -LiteralPath $LogPath -Encoding UTF8
        if ($process.ExitCode -ne 0) {
            Add-Content -LiteralPath $LogPath -Value "ERROR: $Name failed with exit code $($process.ExitCode)." -Encoding UTF8
            exit $process.ExitCode
        }
    }
    catch {
        Add-Content -LiteralPath $LogPath -Value "ERROR: $Name could not run: $($_.Exception.Message)" -Encoding UTF8
        exit 1
    }
}

$buildFlags = @(
    "--disable-build-servers", "-m:1", "-p:MSBuildEnableWorkloadResolver=false",
    "-p:UseSharedCompilation=false", "-p:BuildInParallel=false", "-p:NuGetAudit=false"
)

Invoke-PreflightStep "Release build" (@("build", "TbotUltra.sln", "-c", "Release", "--nologo") + $buildFlags)
Invoke-PreflightStep "Worker tests" (@("test", "src/TbotUltra.Worker.Tests/TbotUltra.Worker.Tests.csproj", "-c", "Release", "--no-build", "--nologo") + $buildFlags)
Invoke-PreflightStep "Desktop tests" (@("test", "src/TbotUltra.Desktop.Tests/TbotUltra.Desktop.Tests.csproj", "-c", "Release", "--no-build", "--nologo") + $buildFlags)

Add-Content -LiteralPath $LogPath -Value "`r`nPreflight passed." -Encoding UTF8
