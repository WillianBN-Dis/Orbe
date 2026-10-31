param([Parameter(Mandatory)][string]$Apk,[string]$Serial='emulator-5554')
$ErrorActionPreference='Stop'
$adb=Join-Path $env:LOCALAPPDATA 'AgendaDev/android-sdk/platform-tools/adb.exe'
$package='br.com.agendafinanceira.qa201'
$output=Join-Path $PSScriptRoot '../artifacts/qa/2.0.1/android-forms';New-Item -ItemType Directory -Force $output|Out-Null
function Adb { $r=& $adb -s $Serial @args 2>&1;if($LASTEXITCODE -ne 0){throw 'ADB QA forms command failed'};return $r }
if($Serial -notmatch '^emulator-\d+$' -or ((Adb emu avd name)-join '') -notmatch 'Orbe_QA_'){throw 'Only owned Orbe QA emulator accepted'}
Adb install -r (Resolve-Path -LiteralPath $Apk).Path | Out-Null
$activity=((Adb shell cmd package resolve-activity --brief $package)|Where-Object {$_ -like "$package/*"}|Select-Object -Last 1).Trim()
if(!$activity){throw 'Separate QA package/activity missing'}
try{
 foreach($mode in 'hours','block','services'){
  Adb shell am force-stop $package | Out-Null
  Adb shell am start -W -n $activity --es orbe.qa.form $mode | Out-Null
  $fixturePid=((Adb shell pidof $package)-join '').Trim()
  if($fixturePid -notmatch '^\d+$'){throw 'QA process missing'}
  $file=$null;$report=$null
  for($n=0;$n -lt 30;$n++){
   Start-Sleep -Milliseconds 500
   $files=Adb shell run-as $package ls cache
   $file=$files|Where-Object {$_ -eq "orbe-forms-$fixturePid.json"}|Select-Object -Last 1
   if($file){$content=(Adb shell run-as $package cat "cache/$file") -join "`n";try{$report=$content|ConvertFrom-Json;if($report.passed -and $report.mode -eq $mode){break}}catch{throw 'Synthetic form fixture failed'}}
  }
  if(!$report.passed -or $report.mode -ne $mode){throw "Form fixture did not pass: $mode"}
  $content|Set-Content (Join-Path $output "$mode.json")
  Adb shell uiautomator dump /sdcard/orbe-forms.xml | Out-Null
  Adb pull /sdcard/orbe-forms.xml (Join-Path $output "$mode.xml") | Out-Null
  Adb shell screencap -p /sdcard/orbe-forms.png | Out-Null
  Adb pull /sdcard/orbe-forms.png (Join-Path $output "$mode.png") | Out-Null
  "PASS Android synthetic form: $mode"
 }
}finally{
 Adb shell am force-stop $package | Out-Null
 Adb uninstall $package | Out-Null
}
