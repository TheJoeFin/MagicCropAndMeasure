<#
.SYNOPSIS
Builds MagickCrop's unpackaged downloads and uploads them to a draft GitHub release.
.EXAMPLE
.\build.ps1
.EXAMPLE
.\build.ps1 -SkipRelease -SkipSigning
.EXAMPLE
.\build.ps1 -Prerelease -Notes 'Release notes'
#>
[CmdletBinding()]
param(
    [string]$Notes,
    [switch]$Prerelease,
    [Alias('Rebuild')]
    [switch]$SkipRelease,
    [switch]$SkipSigning,

    # Uncommitted changes will be built but will not be included in the tag.
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
$RepoRoot = $PSScriptRoot
$Project = 'MagickCrop'
$ProjectPath = Join-Path $RepoRoot 'MagickCrop\MagickCrop.csproj'
$BuildRoot = Join-Path $RepoRoot 'bld'
$PublishRoot = Join-Path $BuildRoot 'publish'
$ArtifactRoot = Join-Path $BuildRoot 'artifacts'

$BuildPathX64 = Join-Path $PublishRoot 'x64'
$BuildPathX64SC = Join-Path $PublishRoot 'x64-self-contained'
$BuildPathArm64 = Join-Path $PublishRoot 'arm64'
$BuildPathArm64SC = Join-Path $PublishRoot 'arm64-self-contained'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$GitArguments)

    $Output = & git -C $RepoRoot @GitArguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
    return $Output
}

function Assert-CleanWorktree {
    if (-not $AllowDirty) {
        $Status = Invoke-Git -GitArguments @('status', '--porcelain')
        if ($Status) {
            throw "Working tree has uncommitted changes; commit/stash them or pass -AllowDirty.`n$($Status -join "`n")"
        }
    }
}

function Invoke-Publish {
    param(
        [Parameter(Mandatory)][string]$Runtime,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][bool]$SelfContained
    )

    $BuildType = if ($SelfContained) { 'self-contained' } else { 'framework-dependent' }
    Write-Host "Building $Runtime $BuildType..." -ForegroundColor Yellow

    $Arguments = @(
        'publish'
        $ProjectPath
        '--runtime', $Runtime
        '--configuration', 'Release'
        '--verbosity', 'minimal'
        '--output', $OutputPath
        '-p:EnableMsixTooling=true'
        '-p:PublishSingleFile=true'
        '-p:CopyOutputSymbolsToPublishDirectory=false'
        '--nologo'
    )

    if ($SelfContained) {
        $Arguments += @(
            '--self-contained'
            '-p:DebugSymbols=false'
            '-p:DebugType=None'
        )
    }
    else {
        $Arguments += '--no-self-contained'
    }

    if ($Runtime -eq 'win-x64') {
        $Arguments += "-p:PublishReadyToRun=$($SelfContained.ToString().ToLowerInvariant())"
    }

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "The $Runtime $BuildType publish failed with exit code $LASTEXITCODE."
    }
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Building MagickCrop requires Windows.'
}

if (-not $SkipRelease -and $SkipSigning) {
    throw 'A GitHub release cannot be created without signing. Omit -SkipSigning or use -SkipRelease.'
}

$RequiredTools = @('dotnet')
if (-not $SkipSigning) {
    $RequiredTools += 'sign-exe'
}
if (-not $SkipRelease) {
    $RequiredTools += 'git', 'gh'
}
foreach ($Tool in $RequiredTools) {
    if (-not (Get-Command $Tool -ErrorAction SilentlyContinue)) {
        if ($Tool -eq 'sign-exe') {
            throw 'sign-exe is required for signed artifacts. Load your signing profile or pass -SkipRelease -SkipSigning for a local build.'
        }
        throw "$Tool is required but was not found on PATH."
    }
}

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf)) {
    throw "Required file not found: $ProjectPath"
}

$VersionOutput = & dotnet msbuild $ProjectPath -nologo -property:Configuration=Release -getProperty:Version
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the app version from MagickCrop.csproj.'
}
$Version = ($VersionOutput | Out-String).Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
    throw "Invalid app version returned by MSBuild: '$Version'."
}
$Tag = "v$Version"

Write-Host "Building MagickCrop $Version..." -ForegroundColor Green

if (-not $SkipRelease) {
    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        throw 'gh is not authenticated. Run "gh auth login" first.'
    }

    $OriginUrl = Invoke-Git -GitArguments @('remote', 'get-url', 'origin')
    if ($OriginUrl -notmatch 'github\.com[:/](?<repo>[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+?)(?:\.git)?/?$') {
        throw "Could not determine the GitHub owner/repository from origin ($OriginUrl)."
    }
    $Repository = $Matches['repo']
    $Commit = Invoke-Git -GitArguments @('rev-parse', '--verify', 'HEAD')
    Assert-CleanWorktree

    Invoke-Git -GitArguments @('fetch', 'origin', '--quiet') | Out-Null
    $RemoteBranches = Invoke-Git -GitArguments @('branch', '--remotes', '--contains', $Commit, '--list', 'origin/*')
    if (-not $RemoteBranches) {
        throw 'HEAD has not been pushed to a branch on origin. Push before releasing.'
    }

    $LocalTag = Invoke-Git -GitArguments @('tag', '--list', $Tag)
    $RemoteTag = Invoke-Git -GitArguments @('ls-remote', '--tags', 'origin', "refs/tags/$Tag")
    if ($LocalTag -or $RemoteTag) {
        throw "Tag $Tag already exists. Bump Version in MagickCrop.csproj before releasing."
    }

    $ReleaseTags = & gh api "repos/$Repository/releases" --paginate --jq '.[].tag_name'
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not check existing GitHub releases.'
    }
    if ($Tag -in $ReleaseTags) {
        throw "A release for $Tag already exists on GitHub (possibly a draft). Delete it or bump Version."
    }
}

Write-Host "`nCleaning previous build output..." -ForegroundColor Cyan
if (Test-Path -LiteralPath $PublishRoot) {
    Remove-Item -LiteralPath $PublishRoot -Recurse -Force
}
if (Test-Path -LiteralPath $ArtifactRoot) {
    Remove-Item -LiteralPath $ArtifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishRoot, $ArtifactRoot -Force | Out-Null

Write-Host "`n=== Building x64 Versions ===" -ForegroundColor Magenta
Invoke-Publish -Runtime 'win-x64' -OutputPath $BuildPathX64 -SelfContained $false
Invoke-Publish -Runtime 'win-x64' -OutputPath $BuildPathX64SC -SelfContained $true

Write-Host "`n=== Building ARM64 Versions ===" -ForegroundColor Magenta
Invoke-Publish -Runtime 'win-arm64' -OutputPath $BuildPathArm64 -SelfContained $false
Invoke-Publish -Runtime 'win-arm64' -OutputPath $BuildPathArm64SC -SelfContained $true

Write-Host "`n=== Collecting Artifacts ===" -ForegroundColor Magenta
$ArtifactX64 = Join-Path $ArtifactRoot "$Project.exe"
$ArtifactArm64 = Join-Path $ArtifactRoot "$Project-arm64.exe"
$ArtifactX64SC = Join-Path $ArtifactRoot "$Project-x64-Self-Contained"
$ArtifactArm64SC = Join-Path $ArtifactRoot "$Project-arm64-Self-Contained"

$SourceX64 = Join-Path $BuildPathX64 "$Project.exe"
$SourceArm64 = Join-Path $BuildPathArm64 "$Project.exe"
foreach ($Executable in $SourceX64, $SourceArm64) {
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
        throw "Expected executable was not produced: $Executable"
    }
}

Move-Item -LiteralPath $SourceX64 -Destination $ArtifactX64
Move-Item -LiteralPath $SourceArm64 -Destination $ArtifactArm64
Move-Item -LiteralPath $BuildPathX64SC -Destination $ArtifactX64SC
Move-Item -LiteralPath $BuildPathArm64SC -Destination $ArtifactArm64SC
Rename-Item -LiteralPath (Join-Path $ArtifactArm64SC "$Project.exe") -NewName "$Project-arm64.exe"

Remove-Item -LiteralPath $PublishRoot -Recurse -Force

if (-not $SkipSigning) {
    Write-Host "`nSigning release executables..." -ForegroundColor Cyan
    & sign-exe -SearchPath $ArtifactRoot
    if ($LASTEXITCODE -ne 0) {
        throw "sign-exe failed with exit code $LASTEXITCODE. No release was created."
    }

    $Executables = @(Get-ChildItem -LiteralPath $ArtifactRoot -Filter '*.exe' -File -Recurse)
    if ($Executables.Count -eq 0) {
        throw "No release executables found in: $ArtifactRoot"
    }
    foreach ($Executable in $Executables) {
        $Signature = Get-AuthenticodeSignature -LiteralPath $Executable.FullName
        if ($Signature.Status -ne 'Valid') {
            throw "Release executable does not have a valid signature: $($Executable.FullName) ($($Signature.Status)). No release was created."
        }
    }
}

foreach ($Directory in $ArtifactX64SC, $ArtifactArm64SC) {
    Write-Host "Creating archive: $(Split-Path $Directory -Leaf).zip" -ForegroundColor Yellow
    Compress-Archive -Path (Join-Path $Directory '*') -DestinationPath "$Directory.zip" -Force
    Remove-Item -LiteralPath $Directory -Recurse -Force
}

$AssetNames = @(
    "$Project.exe"
    "$Project-arm64.exe"
    "$Project-x64-Self-Contained.zip"
    "$Project-arm64-Self-Contained.zip"
)
$Assets = foreach ($Name in $AssetNames) {
    $Path = Join-Path $ArtifactRoot $Name
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Expected release artifact not found: $Path"
    }
    $File = Get-Item -LiteralPath $Path
    if ($File.Length -eq 0) {
        throw "Release artifact is empty: $Path"
    }
    $File
}

Write-Host "`n=== Version Information ===" -ForegroundColor Cyan
$VersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ArtifactX64)
Write-Host "Product Version: $($VersionInfo.ProductVersion)" -ForegroundColor White
Write-Host "File Version: $($VersionInfo.FileVersion)" -ForegroundColor White

if ($SkipRelease) {
    Write-Host "`nBuild completed. Artifacts are in: $ArtifactRoot" -ForegroundColor Green
    return
}

if ((Invoke-Git -GitArguments @('rev-parse', '--verify', 'HEAD')) -ne $Commit) {
    throw 'HEAD changed during the build. No tag or release was created; rebuild from the intended commit.'
}
Assert-CleanWorktree

Write-Host "Creating local tag $Tag at $Commit..."
Invoke-Git -GitArguments @('tag', $Tag, $Commit) | Out-Null

$GhArguments = @(
    'release', 'create', $Tag
    '--repo', $Repository
    '--title', $Tag
    '--target', $Commit
    '--draft'
) + $Assets.FullName

$NotesFile = $null
try {
    if ($Notes) {
        $NotesFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($NotesFile, $Notes, [System.Text.UTF8Encoding]::new($false))
        $GhArguments += '--notes-file', $NotesFile
    }
    else {
        $GhArguments += '--generate-notes'
    }
    if ($Prerelease) {
        $GhArguments += '--prerelease'
    }

    Write-Host "Creating draft GitHub release $Tag..." -ForegroundColor Cyan
    & gh @GhArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Release creation/upload failed. Check GitHub for a partial draft and remove it and the local tag before retrying (git tag -d $Tag)."
    }
}
finally {
    if ($NotesFile) {
        Remove-Item -LiteralPath $NotesFile -Force
    }
}

Write-Host "Draft release $Tag is ready with all four artifacts. Publishing it on GitHub will create the remote tag at $Commit." -ForegroundColor Green
