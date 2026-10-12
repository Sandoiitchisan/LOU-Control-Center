param([string]$Destination='')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(-not $Destination){$Destination=Join-Path $repoRoot 'artifacts/github/LOU-Control-Center'}
$Destination=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $Destination){throw 'Choose a new destination folder to guarantee its contents.'}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
# Explicit allowlist: no working outputs, user settings, recordings, keys or local SDK.
$roots=@('src','tests','assets','docs','licenses','scripts','tools/PawnIO','tools/PresentMon','.github')
foreach($root in $roots){
    Get-ChildItem -LiteralPath (Join-Path $repoRoot $root) -Recurse -File -Force | ForEach-Object {
        $relative=$_.FullName.Substring($repoRoot.Length+1)
        if($relative -match '(^|[\\/])(bin|obj|Data|Review|research|reference|TestResults)([\\/]|$)' -or $_.Extension -in @('.log','.trx','.pfx','.pem','.key')){return}
        $target=Join-Path $Destination $relative
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target
    }
}
foreach($name in @('.gitignore','.gitattributes','AGENTS.md','Directory.Build.props','Lou.ControlCenter.slnx','README.md','LICENSE','THIRD_PARTY_NOTICES.md','CONTRIBUTING.md','SECURITY.md','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $Destination}
$oversized=Get-ChildItem -LiteralPath $Destination -Recurse -File -Force | Where-Object Length -gt 95MB
if($oversized){throw 'File too large for the GitHub repository.'}
Write-Output "Prepared sources: $Destination"
Write-Output 'Attach portable ZIPs and Setup files to GitHub Releases; do not add them to sources.'
