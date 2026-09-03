[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory,
    [string]$VersionTag,
    [datetime]$Timestamp
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts"
}

if (-not $PSBoundParameters.ContainsKey("Timestamp")) {
    $tokyoTimeZone = [TimeZoneInfo]::FindSystemTimeZoneById("Tokyo Standard Time")
    $Timestamp = [TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $tokyoTimeZone)
}

if ([string]::IsNullOrWhiteSpace($VersionTag)) {
    $detectedTag = & git -C $repoRoot describe --tags --exact-match HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($detectedTag)) {
        throw "HEAD has no tag. Create a tag or pass -VersionTag explicitly."
    }
    $VersionTag = $detectedTag.Trim()
}

$safeVersionTag = $VersionTag -replace "[^A-Za-z0-9._-]", "-"
$archiveBaseName = "mmc_pomodoro_timer_{0}_{1}" -f $safeVersionTag, $Timestamp.ToString("yyMMdd_HHmm")
$publishDirectory = Join-Path $OutputDirectory ".publish-$([Guid]::NewGuid().ToString('N'))"
$packageDirectory = Join-Path $OutputDirectory $archiveBaseName
$archivePath = Join-Path $OutputDirectory "$archiveBaseName.zip"
$projectPath = Join-Path $repoRoot "src\PomodoroTimer\PomodoroTimer.csproj"
$manualPath = Join-Path $repoRoot "manual"
$licensePath = Join-Path $repoRoot "LICENSE"

if (-not (Test-Path -LiteralPath $manualPath -PathType Container)) {
    throw "Manual directory was not found: $manualPath"
}
if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw "License file was not found: $licensePath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

try {
    dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        --output $publishDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
    Get-ChildItem -LiteralPath $publishDirectory -File |
        Where-Object Extension -ne ".pdb" |
        Copy-Item -Destination $packageDirectory
    Copy-Item -LiteralPath $manualPath -Destination $packageDirectory -Recurse
    Copy-Item -LiteralPath $licensePath -Destination $packageDirectory

    Compress-Archive -Path (Join-Path $packageDirectory "*") -DestinationPath $archivePath -Force
    Write-Output $archivePath
}
finally {
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
    if (Test-Path -LiteralPath $packageDirectory) {
        Remove-Item -LiteralPath $packageDirectory -Recurse -Force
    }
}
