param(
    [Parameter(Mandatory = $true)]
    [string]$DotnetExe,
    [Parameter(Mandatory = $true)]
    [string]$LogPath,
    [string]$StatusPath,
    [switch]$DebugBuildOnly,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$logDirectory = Split-Path -Parent $LogPath
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Set-Content -LiteralPath $LogPath -Value "Tbot Ultra local CI preflight" -Encoding UTF8

function Write-LaunchStatus {
    param([int]$Progress, [string]$Message)
    if ($StatusPath) {
        Set-Content -LiteralPath $StatusPath -Value "$Progress|$Message" -Encoding UTF8
    }
}

function Invoke-PreflightStep {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [int]$Progress
    )

    Write-LaunchStatus $Progress "$Name in progress..."
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

if ($DebugBuildOnly) {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        Add-Content -LiteralPath $LogPath -Value "ERROR: Desktop debug build output path is missing." -Encoding UTF8
        exit 1
    }

    Invoke-PreflightStep "Desktop debug build" (@("build", "src/TbotUltra.Desktop/TbotUltra.Desktop.csproj", "-c", "Debug", "--nologo", "-o", $OutputPath) + $buildFlags) 86
    Add-Content -LiteralPath $LogPath -Value "`r`nDesktop debug build passed." -Encoding UTF8
    exit 0
}

Invoke-PreflightStep "Release build" (@("build", "TbotUltra.sln", "-c", "Release", "--nologo") + $buildFlags) 12
Invoke-PreflightStep "Worker tests" (@("test", "src/TbotUltra.Worker.Tests/TbotUltra.Worker.Tests.csproj", "-c", "Release", "--no-build", "--nologo") + $buildFlags) 42
Invoke-PreflightStep "Desktop tests" (@("test", "src/TbotUltra.Desktop.Tests/TbotUltra.Desktop.Tests.csproj", "-c", "Release", "--no-build", "--nologo") + $buildFlags) 62

Add-Content -LiteralPath $LogPath -Value "`r`nPreflight passed." -Encoding UTF8
Write-LaunchStatus 82 "Build and tests passed. Preparing the app..."
