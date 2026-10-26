param(
 [Parameter(Mandatory)][string]$Apk,
 [string]$Version='1.0.2',
 [string]$Serial='emulator-5554'
)
$ErrorActionPreference='Stop'
$adb=Join-Path $env:LOCALAPPDATA 'AgendaDev\android-sdk\platform-tools\adb.exe'
$package='br.com.agendafinanceira.app'
$output=Join-Path $PSScriptRoot "../artifacts/qa/android/$Version"
New-Item -ItemType Directory -Force $output | Out-Null
function Adb {
 $result=& $adb -s $Serial @args 2>&1
 if($LASTEXITCODE -ne 0){throw "ADB falhou: $result"}
 return $result
}
if($Serial -notmatch '^emulator-\d+$'){throw 'Use somente emulador isolado de QA.'}
$avd=Adb emu avd name
if(($avd -join '') -notmatch 'Orbe_QA_'){throw 'O AVD deve ter nome Orbe_QA_*.'}
if((Adb shell getprop sys.boot_completed) -notmatch '1'){throw 'Aguarde a inicialização completa do emulador.'}
$existing=(Adb shell pm list packages $package) -join ''
$installMode=if($existing -match [regex]::Escape($package)){'atualização'}else{'instalação limpa'}
Adb install -r (Resolve-Path -LiteralPath $Apk).Path
$installed=(Adb shell dumpsys package $package) -join "`n"
if($installed -notmatch "versionName=$([regex]::Escape($Version))(\s|$)"){throw 'Versão instalada incorreta.'}
$activity=(Adb shell cmd package resolve-activity --brief $package | Select-Object -Last 1).Trim()
Adb logcat -c
function CheckScreen([string]$name,[string]$expected) {
 Start-Sleep -Seconds 3
 $appProcess=Adb shell pidof $package
 if(!$appProcess){throw 'O processo do Orbe encerrou.'}
 $foreground=(Adb shell dumpsys activity activities) -join "`n"
 if($foreground -notmatch "(?:mResumedActivity|topResumedActivity|ResumedActivity:).*$([regex]::Escape($package))"){throw 'O Orbe não está em primeiro plano.'}
 Adb shell uiautomator dump /sdcard/orbe-qa.xml | Out-Null
 Adb pull /sdcard/orbe-qa.xml (Join-Path $output "$name.xml") | Out-Null
 [xml]$tree=Get-Content -LiteralPath (Join-Path $output "$name.xml") -Raw
 if(!($tree.SelectNodes('//node') | Where-Object {$_.'text' -like "*$expected*"})){throw "Tela $name não contém $expected."}
 Adb shell screencap -p /sdcard/orbe-qa.png | Out-Null
 Adb pull /sdcard/orbe-qa.png (Join-Path $output "$name.png") | Out-Null
 return $tree
}
try {
 foreach($round in 1..2){
  Adb shell am force-stop $package | Out-Null
  Adb shell am start -W -n $activity | Out-Null
  $tree=CheckScreen "abertura-$round" 'Tudo no seu tempo.'
 }
 # Fresh QA emulator must present authentication, without demo data or saved tokens.
 if($tree.SelectNodes('//node') | Where-Object {$_.text -eq 'Financeiro'}){throw 'A agenda não deve abrir sem autenticação.'}
 Adb shell input keyevent KEYCODE_HOME | Out-Null
 Start-Sleep -Seconds 2
 Adb shell am start -W -n $activity | Out-Null
 $null=CheckScreen 'retorno' 'Tudo no seu tempo.'
 $crash=(Adb logcat -d -b crash) -join "`n"
 if($crash -match [regex]::Escape($package)){throw 'Logcat contém falha do Orbe.'}
 "PASS: APK $Version, $installMode, duas aberturas com login obrigatório e retorno do segundo plano; Android $(Adb shell getprop ro.build.version.release), ABI $(Adb shell getprop ro.product.cpu.abi)." | Tee-Object -FilePath (Join-Path $output 'resultado.txt')
}finally{
 Adb logcat -d -b crash | Set-Content -LiteralPath (Join-Path $output 'crash.txt')
}
