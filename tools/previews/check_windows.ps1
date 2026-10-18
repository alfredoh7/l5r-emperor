param([Parameter(Mandatory=$true)][string]$ReceiptPath)
$ErrorActionPreference = 'Stop'
$receipt = Get-Content -Raw -LiteralPath $ReceiptPath | ConvertFrom-Json
if ($receipt.repository -notin @('l5r-emperor-dev/l5r-emperor','alfredoh7/l5r-emperor')) { throw 'Unexpected repository' }
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
$bundleRoot = $null
if ($receipt.companions) {
  $bundleRoot = $root
  foreach ($part in $receipt.companions) {
    if ([System.IO.Path]::GetFileName($part.asset) -ne $part.asset -or $part.sha256 -notmatch '^[a-f0-9]{64}$') { throw 'Invalid companion' }
    & gh release download $receipt.tag --repo $receipt.repository --pattern $part.asset --dir $scratch
    if ($LASTEXITCODE -ne 0) { throw 'Companion download failed' }
    $partArchive = Join-Path $scratch $part.asset
    if ((Get-Item -LiteralPath $partArchive).Length -ne $part.bytes -or (Get-FileHash -LiteralPath $partArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $part.sha256) { throw 'Companion checksum mismatch' }
    Expand-Archive -LiteralPath $partArchive -DestinationPath $expanded -Force
  }
  & (Join-Path $root 'Prepare Review.ps1') -BundleRoot $root -AssembleOnly
  if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Preview preparation failed' }
  $root = Join-Path (Join-Path $root 'payload') $receipt.folder
}
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
$userFolder = Join-Path $env:APPDATA ('L5R\Previews\' + $receipt.folder.Substring('L5R-Review-'.Length))
if (-not (Test-Path -LiteralPath $userFolder)) { throw 'Export did not create its isolated user-data directory' }
Write-Host 'WINDOWS_RUNTIME_PASS: exported executable starts with bundled runtime and resources'

# Execute every actual .cmd launcher against a small local executable that records
# its arguments. This catches cmd quoting/START/CWD failures on real Windows,
# including an extraction folder containing spaces, ampersands and exclamations.
# Do not rename/replace the real executable after its successful runtime check:
# Windows can briefly retain a file handle after process exit. Route byte-identical
# copies of the launchers in a separate scratch tree, leaving the real export intact.
$launcherRoot = Join-Path $scratch 'launcher routing space & bang!'
$launcherGame = Join-Path $launcherRoot 'game'
New-Item -ItemType Directory -Path $launcherGame -Force | Out-Null
foreach ($scenario in $spec.scenarios) {
  $sourceLauncher = Join-Path $root ($scenario.name + '.cmd')
  $copiedLauncher = Join-Path $launcherRoot ($scenario.name + '.cmd')
  New-Item -ItemType Directory -Path (Split-Path $copiedLauncher) -Force | Out-Null
  Copy-Item -LiteralPath $sourceLauncher -Destination $copiedLauncher
  if ((Get-FileHash -LiteralPath $sourceLauncher).Hash -ne (Get-FileHash -LiteralPath $copiedLauncher).Hash) { throw 'Launcher copy differs from package' }
}
# The routing stub reads no resources; this marker satisfies the launcher's existence
# guard. Actual PCK/resources were exercised above by the real exported executable.
New-Item -ItemType File -Path (Join-Path $launcherGame 'L5R-Emperor.pck') | Out-Null
$stubExe = Join-Path $launcherGame 'L5R-Emperor.exe'
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
& $compiler /nologo /target:winexe "/out:$stubExe" $stubSource
if ($LASTEXITCODE -ne 0) { throw 'Launcher test stub compile failed' }
$marker = Join-Path $scratch 'arguments.txt'
$env:L5R_PREVIEW_ARGUMENTS_TEST = $marker
foreach ($scenario in $spec.scenarios) {
  if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
  $launcher = Join-Path $launcherRoot ($scenario.name + '.cmd')
  # Run through cmd's actual double-click command handling from an unrelated CWD.
  $start = New-Object System.Diagnostics.ProcessStartInfo
  $start.FileName = $env:ComSpec
  # Pass the exact CreateProcess command line: PowerShell's native marshalling
  # otherwise strips the cmd /c wrapper quotes around a path containing &.
  $start.Arguments = '/d /s /c ""' + $launcher + '""'
  $start.UseShellExecute = $false
  $launchProcess = [System.Diagnostics.Process]::Start($start)
  if (-not $launchProcess.WaitForExit(15000)) { $launchProcess.Kill(); throw 'Launcher hung' }
  if ($launchProcess.ExitCode -ne 0) { throw "Launcher failed: $($scenario.name)" }
  $deadline = (Get-Date).AddSeconds(15)
  while (-not (Test-Path -LiteralPath $marker) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
  if (-not (Test-Path -LiteralPath $marker)) { throw "Launcher did not start executable: $($scenario.name)" }
  $lines = @(Get-Content -LiteralPath $marker)
  if ($lines[0] -ne $launcherGame) { throw 'Launcher used wrong working directory' }
  if ($lines -notcontains '--') { throw 'User-argument separator missing' }
  if ($lines -notcontains ('--profile=review-' + $receipt.source_commit.Substring(0,10))) { throw 'Review profile missing' }
  if ($null -ne $scenario.mode) {
    if ($lines -notcontains ('--capture-mode=' + $scenario.mode)) { throw 'Wrong prepared scenario' }
    $capture = @($lines | Where-Object { $_.StartsWith('--capture=') })
    if ($capture.Count -ne 1 -or -not (Test-Path -LiteralPath (Split-Path $capture[0].Substring(10)))) { throw 'Capture output path is not writable/prepared' }
  } elseif (@($lines | Where-Object { $_.StartsWith('--capture=') }).Count) { throw 'Normal game unexpectedly enters a fixture' }
  Write-Host "WINDOWS_LAUNCHER_PASS: $($scenario.name)"
}
if ($bundleRoot) {
  # Exercise the byte-identical outer one-click launcher, including its inline
  # PowerShell invocation, against the same argument recorder after assembly.
  $bootstrapRoot = Join-Path $scratch 'bootstrap routing space & bang!'
  $bootstrapPayload = Join-Path (Join-Path $bootstrapRoot 'payload') $receipt.folder
  New-Item -ItemType Directory -Path (Split-Path $bootstrapPayload) -Force | Out-Null
  Copy-Item -LiteralPath $launcherRoot -Destination $bootstrapPayload -Recurse
  Copy-Item -LiteralPath (Join-Path $root 'preview.json') -Destination $bootstrapPayload
  foreach ($name in @('Start Review.cmd', 'Prepare Review.ps1', 'review-bundle.json')) {
    Copy-Item -LiteralPath (Join-Path $bundleRoot $name) -Destination $bootstrapRoot
  }
  Remove-Item -LiteralPath $marker -ErrorAction SilentlyContinue
  $start = New-Object System.Diagnostics.ProcessStartInfo
  $start.FileName = $env:ComSpec
  $start.Arguments = '/d /s /c ""' + (Join-Path $bootstrapRoot 'Start Review.cmd') + '""'
  $start.UseShellExecute = $false
  $bootstrapProcess = [System.Diagnostics.Process]::Start($start)
  if (-not $bootstrapProcess.WaitForExit(30000)) { $bootstrapProcess.Kill(); throw 'Bundle launcher hung' }
  if ($bootstrapProcess.ExitCode -ne 0) { throw 'Bundle launcher failed' }
  $deadline = (Get-Date).AddSeconds(15)
  while (-not (Test-Path -LiteralPath $marker) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
  if (-not (Test-Path -LiteralPath $marker)) { throw 'Bundle launcher did not open the prepared scenario' }
  $lines = @(Get-Content -LiteralPath $marker)
  if ($lines[0] -ne (Join-Path $bootstrapPayload 'game') -or $lines -notcontains ('--capture-mode=' + $spec.scenarios[0].mode)) { throw 'Bundle launcher changed the destination/scenario' }
  Write-Host 'WINDOWS_BUNDLE_LAUNCHER_PASS: outer click opens exact prepared game under paths with spaces, & and !'
}
Remove-Item Env:L5R_PREVIEW_ARGUMENTS_TEST
# The release archive and its real extracted executable were never changed.
Write-Host 'WINDOWS_PREVIEW_PASS: checksum, runtime, isolated package and all scenario launchers'

# Each receipt owns this scratch tree; release it before validating the next large package.
try { Remove-Item -LiteralPath $scratch -Recurse -Force } catch { Write-Warning ('Temporary preview cleanup: ' + $_) }
