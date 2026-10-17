param([string]$DotNetPath='dotnet')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $repo
try {
    & $DotNetPath test tests/Lou.Diagnostics.Tests/Lou.Diagnostics.Tests.csproj -c Release --logger 'trx;LogFileName=diagnostics.trx' --results-directory artifacts/tests
    if($LASTEXITCODE -ne 0){throw 'Diagnostics tests failed'}
    $stage=Join-Path $repo ('artifacts/diagnostics-build-'+[Guid]::NewGuid().ToString('N'))
    & $DotNetPath publish src/Lou.Diagnostics/Lou.Diagnostics.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o $stage
    if($LASTEXITCODE -ne 0){throw 'Diagnostics publish failed'}
    $target=Join-Path $repo 'artifacts/LOU-Diagnostics.exe'
    Copy-Item -LiteralPath (Join-Path $stage 'LOU.Diagnostics.exe') -Destination $target -Force
    ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash+'  LOU-Diagnostics.exe') | Set-Content -LiteralPath ($target+'.sha256') -Encoding ascii
    Write-Output $target
} finally { Pop-Location }
