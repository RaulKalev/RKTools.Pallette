<#
.SYNOPSIS
    Runs the Palette in-Revit self-test (Palette.Diagnostics.SelfTest) in a fresh Revit session.

.DESCRIPTION
    1. Writes a temporary per-user add-in manifest pointing at the built Palette.dll.
    2. Starts Revit with PALETTE_SELFTEST_OUTPUT set. The self-test creates its own scratch project in the output folder
       (it never opens your projects), opens Palette, drives it with key events, runs a command, writes
       selftest-results.txt and closes Revit.
    3. Removes the temporary manifest and prints the results.

    The test moves the mouse cursor to the middle and to the bottom-right corner of the monitor under it, to check the
    placement. Revit asks once whether to load the unsigned Palette add-in: choose "Load Once" (or "Always Load").

.EXAMPLE
    ./Tools/Run-RevitSelfTest.ps1 -RevitVersion 2026 -RevitExe "E:\Revit 2026\Revit.exe"
#>
param(
    [ValidateSet("2024", "2026")] [string] $RevitVersion = "2026",
    [string] $RevitExe,
    [string] $Output = (Join-Path $env:TEMP "PaletteSelfTest"),
    [int] $TimeoutMinutes = 15
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$framework = if ($RevitVersion -eq "2024") { "net48" } else { "net8.0-windows" }
$dll = Join-Path $repo "Palette\bin\Release\$framework\Palette.dll"
if (-not (Test-Path $dll)) {
    throw "Build first: dotnet build Palette/Palette.csproj -c Release -p:PaletteUseDefaultOutput=true (missing $dll)"
}

if (-not $RevitExe) {
    $candidates = @(
        "C:\Program Files\Autodesk\Revit $RevitVersion\Revit.exe",
        "D:\Revit $RevitVersion\Revit.exe",
        "E:\Revit $RevitVersion\Revit.exe")
    $RevitExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $RevitExe) { throw "Revit.exe not found; pass -RevitExe." }
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null
$results = Join-Path $Output "selftest-results.txt"
if (Test-Path $results) { Remove-Item $results }

$addinDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
New-Item -ItemType Directory -Force -Path $addinDir | Out-Null
$manifest = Join-Path $addinDir "Palette.SelfTest.addin"
@"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Palette (self-test)</Name>
    <Assembly>$dll</Assembly>
    <FullClassName>Palette.App</FullClassName>
    <AddInId>A661209B-2F28-4D4D-B721-381C5B2D8464</AddInId>
    <VendorId>RaulKalev</VendorId>
    <VendorDescription>Raul Kalev (RK Tools)</VendorDescription>
  </AddIn>
</RevitAddIns>
"@ | Set-Content -Path $manifest -Encoding UTF8

try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo $RevitExe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables["PALETTE_SELFTEST_OUTPUT"] = $Output
    $psi.EnvironmentVariables["PALETTE_SELFTEST_EXIT"] = "1"
    Write-Host "Starting $RevitExe (self-test output: $Output) ..."
    $proc = [System.Diagnostics.Process]::Start($psi)

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while (-not (Test-Path $results) -and (Get-Date) -lt $deadline -and -not $proc.HasExited) { Start-Sleep -Seconds 5 }

    if (Test-Path $results) {
        Get-Content $results
        if (-not $proc.WaitForExit(120000)) { Write-Warning "Revit did not exit by itself; close it manually." }
    } else {
        Write-Warning "No results after $TimeoutMinutes min (was the unsigned add-in prompt answered?). Revit left running."
    }
}
finally {
    Remove-Item $manifest -ErrorAction SilentlyContinue
}
