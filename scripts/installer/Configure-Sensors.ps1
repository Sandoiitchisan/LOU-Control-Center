param([switch]$Remove)
$ErrorActionPreference='Stop'
$taskName='LOU Control Center Universal - Capteurs'
$installRoot=[IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'LOU Control Center Universal'))
if([IO.Path]::GetFullPath($PSCommandPath) -ne (Join-Path $installRoot 'setup\Configure-Sensors.ps1')){throw 'Exécuter uniquement depuis le dossier installé protégé.'}
$logRoot=Join-Path $env:ProgramData 'LouControlCenterUniversal\Sensors'
if($Remove){
    $task=Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if($task){Stop-ScheduledTask -TaskName $taskName; Unregister-ScheduledTask -TaskName $taskName -Confirm:$false}
    $presentMon=Join-Path $installRoot 'tools\PresentMon\PresentMon.exe'
    if(Test-Path -LiteralPath $presentMon){$stopper=Start-Process -FilePath $presentMon -ArgumentList '--session_name','LouControlCenterUniversal-Machine','--terminate_existing_session' -WindowStyle Hidden -PassThru -Wait}
    exit 0
}
# Refuse redirected directories before the elevated collector writes any file.
foreach($path in @($installRoot,(Join-Path $env:ProgramData 'LouControlCenterUniversal'),$logRoot)){
    if((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Dossier redirigé non autorisé.'}
}
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$acl=New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($entry in @(@('S-1-5-18','FullControl'),@('S-1-5-32-544','FullControl'),@('S-1-5-32-545','ReadAndExecute'))){
    $sid=New-Object System.Security.Principal.SecurityIdentifier($entry[0])
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($sid,$entry[1],'ContainerInherit,ObjectInherit','None','Allow')))
}
Set-Acl -LiteralPath (Split-Path $logRoot -Parent) -AclObject $acl
Set-Acl -LiteralPath $logRoot -AclObject $acl
try{
    $pawn=Join-Path $installRoot 'tools\PawnIO\PawnIO_setup.exe'
    $version=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO' -ErrorAction SilentlyContinue).DisplayVersion
    if(-not $version -or [version]$version -lt [version]'2.2.0'){
        if((Get-FileHash -LiteralPath $pawn -Algorithm SHA256).Hash -ne '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'){throw 'Empreinte PawnIO incorrecte.'}
        $signature=Get-AuthenticodeSignature -LiteralPath $pawn
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=namazso.eu(?:,|$)'){throw 'Signature PawnIO incorrecte.'}
        $driver=Start-Process -FilePath $pawn -ArgumentList '-install','-silent' -WindowStyle Hidden -PassThru -Wait
        if($driver.ExitCode -notin @(0,3010)){throw "Installation PawnIO : erreur $($driver.ExitCode)."}
        if($driver.ExitCode -eq 3010){Set-Content -LiteralPath (Join-Path $logRoot 'restart-required.txt') -Value 'Redémarrage Windows nécessaire pour PawnIO.'}
    }
    $action=New-ScheduledTaskAction -Execute (Join-Path $installRoot 'Lou.ControlCenter.exe') -Argument '--sensor-collector --machine' -WorkingDirectory $installRoot
    $trigger=New-ScheduledTaskTrigger -AtStartup
    $principal=New-ScheduledTaskPrincipal -UserId 'S-1-5-18' -LogonType ServiceAccount -RunLevel Highest
    $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Lecture seule des températures et vitesses. Aucun réglage de ventilateur.' -Force | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Set-Content -LiteralPath (Join-Path $logRoot 'installation.log') -Value 'Collecteur installé et démarré. Les réglages personnels ne sont pas modifiés.'
}catch{
    Set-Content -LiteralPath (Join-Path $logRoot 'installation.log') -Value $_.Exception.Message
    exit 1
}
