param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [string]$Tag = "",
    [string]$ReleaseTitle = "",
    [string]$Repository = "",
    [string]$OutputRoot = ".\release",
    [string]$PackageLabel = "",
    [string]$NotesFile = "",
    [switch]$GenerateNotes,
    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$ForcePackage,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit=$LASTEXITCODE): $FilePath $($ArgumentList -join ' ')"
    }
}

function Get-DefaultTag {
    $manifestPath = Join-Path $repoRoot "mods_src\HS2VoiceReplaceRuntimeTemplate\manifest.xml"
    if (Test-Path $manifestPath) {
        try {
            [xml]$manifest = Get-Content $manifestPath -Raw -Encoding UTF8
            $version = $manifest.manifest.version
            if (-not [string]::IsNullOrWhiteSpace($version)) {
                return "v$($version.Trim())"
            }
        }
        catch {
        }
    }

    return "v0.0.0"
}

function Get-DefaultReleaseTitle {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedTag
    )

    return "HS2VoiceReplace $ResolvedTag"
}

function Resolve-GitHubRepository {
    param(
        [string]$ExplicitRepository
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitRepository)) {
        return $ExplicitRepository.Trim()
    }

    $originUrl = (& git config --get remote.origin.url).Trim()
    if ([string]::IsNullOrWhiteSpace($originUrl)) {
        throw "Could not resolve GitHub repository from origin."
    }

    if ($originUrl -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^/.]+?)(?:\.git)?$') {
        return "$($Matches.owner)/$($Matches.repo)"
    }

    throw "Origin is not a supported GitHub remote: $originUrl"
}

function Get-GitHubToken {
    foreach ($name in @("GITHUB_TOKEN", "GH_TOKEN")) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }

    throw "Set GITHUB_TOKEN or GH_TOKEN before publishing a GitHub release."
}

function Invoke-GitHubJsonApi {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST", "PATCH", "DELETE")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [object]$Body,
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    $headers = @{
        "Authorization" = "Bearer $Token"
        "Accept" = "application/vnd.github+json"
        "User-Agent" = "HS2VoiceReplace publish_github_release.ps1"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    $params = @{
        Method = $Method
        Uri = $Uri
        Headers = $headers
    }

    if ($PSBoundParameters.ContainsKey("Body") -and $null -ne $Body) {
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }

    try {
        return Invoke-RestMethod @params
    }
    catch {
        $statusCode = $null
        try {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        catch {
        }

        if ($statusCode -eq 404) {
            return $null
        }

        throw
    }
}

function Remove-ReleaseAssetIfPresent {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Release,
        [Parameter(Mandatory = $true)]
        [string]$AssetName,
        [Parameter(Mandatory = $true)]
        [string]$RepositoryName,
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    foreach ($asset in @($Release.assets)) {
        if ($null -eq $asset) { continue }
        if (-not [string]::Equals($asset.name, $AssetName, [System.StringComparison]::OrdinalIgnoreCase)) { continue }

        $uri = "https://api.github.com/repos/$RepositoryName/releases/assets/$($asset.id)"
        Invoke-GitHubJsonApi -Method DELETE -Uri $uri -Token $Token | Out-Null
        return
    }
}

function Upload-ReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Release,
        [Parameter(Mandatory = $true)]
        [string]$AssetPath,
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    $assetName = Split-Path -Leaf $AssetPath
    $uploadUrl = [string]$Release.upload_url
    $uploadBase = $uploadUrl -replace '\{\?name,label\}$', ''
    $uploadUri = "${uploadBase}?name=$([System.Uri]::EscapeDataString($assetName))"

    Invoke-RestMethod `
        -Method POST `
        -Uri $uploadUri `
        -Headers @{
            "Authorization" = "Bearer $Token"
            "Accept" = "application/vnd.github+json"
            "User-Agent" = "HS2VoiceReplace publish_github_release.ps1"
            "X-GitHub-Api-Version" = "2022-11-28"
        } `
        -ContentType "application/zip" `
        -InFile $AssetPath | Out-Null
}

$resolvedTag = if ([string]::IsNullOrWhiteSpace($Tag)) { Get-DefaultTag } else { $Tag.Trim() }
$resolvedTitle = if ([string]::IsNullOrWhiteSpace($ReleaseTitle)) { Get-DefaultReleaseTitle -ResolvedTag $resolvedTag } else { $ReleaseTitle.Trim() }
$resolvedRepository = Resolve-GitHubRepository -ExplicitRepository $Repository

$packageScriptPath = Join-Path $repoRoot "tools\package_release.ps1"
$packageParameters = @{
    GameRoot = $GameRoot
    OutputRoot = $OutputRoot
    PassThru = $true
}
if (-not [string]::IsNullOrWhiteSpace($PackageLabel)) {
    $packageParameters.PackageLabel = $PackageLabel
}
if ($ForcePackage) {
    $packageParameters.Force = $true
}

$packageInfo = & $packageScriptPath @packageParameters
if ($null -eq $packageInfo -or [string]::IsNullOrWhiteSpace($packageInfo.ZipPath)) {
    throw "package_release.ps1 did not return a release zip path."
}

$zipPath = [string]$packageInfo.ZipPath
$assetName = Split-Path -Leaf $zipPath

Write-Host "[info] Repository: $resolvedRepository"
Write-Host "[info] Tag: $resolvedTag"
Write-Host "[info] Title: $resolvedTitle"
Write-Host "[info] Asset: $zipPath"

if ($DryRun) {
    Write-Host "[dry-run] Packaging completed. Skipping GitHub release upload."
    exit 0
}

$token = Get-GitHubToken
$currentCommit = (& git rev-parse HEAD).Trim()
$notesBody = $null
if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    $notesBody = Get-Content $NotesFile -Raw -Encoding UTF8
}

$releaseUri = "https://api.github.com/repos/$resolvedRepository/releases/tags/$resolvedTag"
$release = Invoke-GitHubJsonApi -Method GET -Uri $releaseUri -Token $token

if ($null -eq $release) {
    $createBody = @{
        tag_name = $resolvedTag
        target_commitish = $currentCommit
        name = $resolvedTitle
        draft = [bool]$Draft
        prerelease = [bool]$Prerelease
        generate_release_notes = [bool]($GenerateNotes -and [string]::IsNullOrWhiteSpace($notesBody))
    }

    if (-not [string]::IsNullOrWhiteSpace($notesBody)) {
        $createBody.body = $notesBody
    }

    $release = Invoke-GitHubJsonApi `
        -Method POST `
        -Uri "https://api.github.com/repos/$resolvedRepository/releases" `
        -Body $createBody `
        -Token $token
}
else {
    $updateBody = @{
        name = $resolvedTitle
        draft = [bool]$Draft
        prerelease = [bool]$Prerelease
    }

    if (-not [string]::IsNullOrWhiteSpace($notesBody)) {
        $updateBody.body = $notesBody
    }

    $release = Invoke-GitHubJsonApi `
        -Method PATCH `
        -Uri "https://api.github.com/repos/$resolvedRepository/releases/$($release.id)" `
        -Body $updateBody `
        -Token $token
}

Remove-ReleaseAssetIfPresent `
    -Release $release `
    -AssetName $assetName `
    -RepositoryName $resolvedRepository `
    -Token $token

Upload-ReleaseAsset -Release $release -AssetPath $zipPath -Token $token

Write-Host "[done] Uploaded $assetName to release $resolvedTag"
