param([string]$DotNetPath='dotnet',[switch]$SkipTests,[switch]$Installer,[string]$InnoCompiler='')
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
$repoRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoRoot
$version=([xml](Get-Content -LiteralPath 'src/Lou.App/Lou.App.csproj' -Raw)).Project.PropertyGroup.Version
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Version de package invalide'}
if((Get-FileHash -LiteralPath 'tools/PresentMon/PresentMon.exe' -Algorithm SHA256).Hash -ne 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF'){throw 'Empreinte PresentMon incorrecte'}
& $DotNetPath build 'Lou.ControlCenter.slnx' -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Compilation échouée'}
if(-not $SkipTests){& $DotNetPath test 'tests/Lou.Tests/Lou.Tests.csproj' -c Release --no-build --logger 'trx;LogFileName=universal.trx' --results-directory 'artifacts/tests';if($LASTEXITCODE -ne 0){throw 'Tests échoués'}}
# A fresh staging directory prevents local Data/ and QA captures entering the ZIP.
$stage=Join-Path $repoRoot ('artifacts/staging-'+[Guid]::NewGuid().ToString('N'))
& $DotNetPath publish 'src/Lou.App/Lou.App.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o $stage
if($LASTEXITCODE -ne 0){throw 'Publication échouée'}
foreach($file in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath $file -Destination $stage}
Copy-Item -LiteralPath 'docs','licenses' -Destination $stage -Recurse
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'tools/PawnIO') | Out-Null
if((Get-FileHash -LiteralPath 'tools/PawnIO/PawnIO_setup.exe' -Algorithm SHA256).Hash -ne '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'){throw 'Empreinte PawnIO incorrecte'}
Get-ChildItem -LiteralPath 'tools/PawnIO' -File | Copy-Item -Destination (Join-Path $stage 'tools/PawnIO')
$portable=Join-Path $repoRoot 'artifacts/portable'
if(Test-Path -LiteralPath $portable){
    # Preserve a previously tested portable folder (including its user settings).
    $resolved=[IO.Path]::GetFullPath($portable)
    if([IO.Path]::GetDirectoryName($resolved) -ne (Join-Path $repoRoot 'artifacts')){throw 'Destination inattendue'}
    Move-Item -LiteralPath $resolved -Destination (Join-Path $repoRoot ('artifacts/portable-previous-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')))
}
$resolvedStage=[IO.Path]::GetFullPath($stage)
if([IO.Path]::GetDirectoryName($resolvedStage) -ne (Join-Path $repoRoot 'artifacts') -or [IO.Path]::GetFileName($resolvedStage) -notmatch '^staging-[0-9a-f]{32}$'){throw 'Chemin de publication inattendu'}
Move-Item -LiteralPath $resolvedStage -Destination $portable
if(Test-Path -LiteralPath (Join-Path $portable 'Data')){throw 'Configuration utilisateur inattendue dans le package'}
$zip=Join-Path $repoRoot "artifacts/LOU-Control-Center-Portable-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force
Set-Content -LiteralPath ($zip+'.sha256') -Value ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash+'  '+[IO.Path]::GetFileName($zip)) -Encoding ascii
Write-Output "Version portable : $portable"
Write-Output "Archive : $zip"
if($Installer){
    if(-not $InnoCompiler){$InnoCompiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'}
    if(-not (Test-Path -LiteralPath $InnoCompiler)){throw 'Inno Setup 6 requis pour compiler le Setup. Utiliser -InnoCompiler pour son chemin.'}
    & $InnoCompiler "/DAppVersion=$version" 'scripts/installer/Lou.iss'
    if($LASTEXITCODE -ne 0){throw 'Compilation installateur échouée'}
    $setup=Join-Path $repoRoot "artifacts/LOU-Control-Center-Setup-$version-win-x64.exe"
    Set-Content -LiteralPath ($setup+'.sha256') -Value ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash+'  '+[IO.Path]::GetFileName($setup)) -Encoding ascii
    Write-Output "Installateur : $setup"
}
