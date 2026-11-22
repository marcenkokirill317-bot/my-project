$root = Split-Path -Parent $PSScriptRoot
$name = "MSC-Multiplayer-FULL-0.2.0-dev"
$stage = Join-Path $env:TEMP $name
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Get-ChildItem $root -Recurse -File |
  Where-Object {
    $_.FullName -notmatch '\\(bin|obj|\.git)\\' -and
    $_.Name -notmatch '^(MSCLoader|UnityEngine|Assembly-CSharp).*\.dll$' -and
    $_.Extension -ne '.zip'
  } |
  ForEach-Object {
    $rel = $_.FullName.Substring($root.Length).TrimStart('\')
    $dst = Join-Path $stage $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
    Copy-Item $_.FullName $dst
  }
$out = Join-Path $root "$name.zip"
if (Test-Path $out) { Remove-Item $out -Force }
Compress-Archive -Path $stage -DestinationPath $out
Write-Host "Gotovo: $out"