param([Parameter(Mandatory=$true)][string]$ReceiptPath)
$ErrorActionPreference = 'Stop'
$receipt = Get-Content -Raw -LiteralPath $ReceiptPath | ConvertFrom-Json
if ($receipt.repository -ne 'alfredoh7/l5r-emperor') { throw 'Unexpected repository' }
if ($receipt.sha256 -notmatch '^[a-f0-9]{64}$') { throw 'Invalid checksum' }
$scratch = Join-Path $env:RUNNER_TEMP ('L5R review space & bang! ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
& gh release download $receipt.tag --repo $receipt.repository --pattern $receipt.asset --dir $scratch
if ($LASTEXITCODE -ne 0) { throw 'Preview download failed' }
$archive = Join-Path $scratch $receipt.asset
if ((Get-Item -LiteralPath $archive).Length -ne $receipt.bytes) { throw 'Size mismatch' }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.sha256) { throw 'SHA-256 mismatch' }
$expanded = Join-Path $scratch 'expanded'
Expand-Archive -LiteralPath $archive -DestinationPath $expanded
$root = Join-Path $expanded $receipt.folder
$spec = Get-Content -Raw -LiteralPath (Join-Path $root 'preview.json') | ConvertFrom-Json
if ($spec.source_commit -ne $receipt.source_commit) { throw 'Source version mismatch' }
$exe = Join-Path $root 'game\L5R-Emperor.exe'
$runtimeLog = Join-Path $scratch 'runtime.log'
# Execute the actual exported .NET Windows game, not the editor or a source build.
# Headless startup verifies runtime loading, not graphical or gameplay acceptance.
$process = Start-Process -FilePath $exe -WorkingDirectory (Join-Path $root 'game') -ArgumentList @('--headless', '--quit-after', '60', '--log-file', ('"' + $runtimeLog + '"')) -PassThru
if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Export runtime startup timed out' }
if ($process.ExitCode -ne 0) { throw "Export runtime exited $($process.ExitCode)" }
$runtimeText = Get-Content -Raw -LiteralPath $runtimeLog
if ($runtimeText -match 'ERROR:|SCRIPT ERROR:|Unhandled exception') { throw $runtimeText }
Write-Host 'WINDOWS_RUNTIME_PASS: exported executable starts with bundled runtime and resources'

# Execute every actual .cmd launcher against a small local executable that records
# its arguments. This catches cmd quoting/START/CWD failures on real Windows,
# including an extraction folder containing spaces, ampersands and exclamations.
$realExe = Join-Path $root 'game\L5R-Emperor-real.exe'
Move-Item -LiteralPath $exe -Destination $realExe
$stubSource = Join-Path $scratch 'CaptureArgs.cs'
@'
using System;
using System.IO;
class CaptureArgs {
  static void Main(string[] args) {
    var marker = Environment.GetEnvironmentVariable("L5R_PREVIEW_ARGUMENTS_TEST");
    var lines = new string[args.Length + 1];
    lines[0] = Environment.CurrentDirectory;
    Array.Copy(args, 0, lines, 1, args.Length);
    File.WriteAllLines(marker + ".tmp", lines);
    File.Move(marker + ".tmp", marker);
  }
}
'@ | Set-Content -LiteralPath $stubSource
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe "/out:$exe" $stubSource
if ($LASTEXITCODE -ne 0) { throw 'Launcher test stub compile failed' }
$marker = Join-Path $scratch 'arguments.txt'
$env:L5R_PREVIEW_ARGUMENTS_TEST = $marker
foreach ($scenario in $spec.scenarios) {
  if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
  $launcher = Join-Path $root ($scenario.name + '.cmd')
  # Run through cmd's actual double-click command handling from an unrelated CWD.
  & $env:ComSpec /d /c "`"$launcher`""
  if ($LASTEXITCODE -ne 0) { throw "Launcher failed: $($scenario.name)" }
  $deadline = (Get-Date).AddSeconds(15)
  while (-not (Test-Path -LiteralPath $marker) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
  if (-not (Test-Path -LiteralPath $marker)) { throw "Launcher did not start executable: $($scenario.name)" }
  $lines = @(Get-Content -LiteralPath $marker)
  if ($lines[0] -ne (Join-Path $root 'game')) { throw 'Launcher used wrong working directory' }
  if ($lines -notcontains '--') { throw 'User-argument separator missing' }
  if ($lines -notcontains ('--profile=review-' + $receipt.source_commit.Substring(0,10))) { throw 'Review profile missing' }
  if ($null -ne $scenario.mode) {
    if ($lines -notcontains ('--capture-mode=' + $scenario.mode)) { throw 'Wrong prepared scenario' }
    $capture = @($lines | Where-Object { $_.StartsWith('--capture=') })
    if ($capture.Count -ne 1 -or -not (Test-Path -LiteralPath (Split-Path $capture[0].Substring(10)))) { throw 'Capture output path is not writable/prepared' }
  } elseif (@($lines | Where-Object { $_.StartsWith('--capture=') }).Count) { throw 'Normal game unexpectedly enters a fixture' }
  Write-Host "WINDOWS_LAUNCHER_PASS: $($scenario.name)"
}
Remove-Item Env:L5R_PREVIEW_ARGUMENTS_TEST
# Only this temporary extraction was modified; never modify the release archive.
Remove-Item -LiteralPath $exe
Move-Item -LiteralPath $realExe -Destination $exe
Write-Host 'WINDOWS_PREVIEW_PASS: checksum, runtime, isolated package and all scenario launchers'
