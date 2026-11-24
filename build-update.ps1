<#
.SYNOPSIS
    Builds the update package (zip) for Invoice Manager from this source code.

.DESCRIPTION
    1. Reads the version from "Invoice Management System.csproj" (<Version>).
    2. dotnet publish -c Release (framework-dependent, same as previous releases).
    3. Creates artifacts\update_v<version>.zip with the files at the ROOT of the zip
       (the installed Updater extracts it directly into the install folder).
    4. Safety checks - the build FAILS if the zip contains:
         - any *.db file   (would overwrite the customer's database on update)
         - an Updater\ folder (the running Updater.exe is locked and the update would fail half-way)

    Release steps: see RELEASING.md

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build-update.ps1
#>
$ErrorActionPreference = 'Stop'

$root    = $PSScriptRoot
$project = Join-Path $root 'Invoice Management System\Invoice Management System.csproj'
[xml]$csproj = Get-Content -LiteralPath $project -Encoding UTF8
$version = ($csproj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "No <Version> found in $project" }
$version = $version.Trim()

$artifacts = Join-Path $root 'artifacts'
$publish   = Join-Path $artifacts "publish_v$version"
$zip       = Join-Path $artifacts "update_v$version.zip"

Write-Host "Invoice Manager $version" -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
if (Test-Path $zip)     { Remove-Item -Force $zip }
New-Item -ItemType Directory -Force $artifacts | Out-Null

Write-Host "dotnet publish -c Release ..."
& dotnet publish $project -c Release -o $publish -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Safety: never ship a database or the Updater folder
$db = Get-ChildItem -LiteralPath $publish -Recurse -Filter *.db -ErrorAction SilentlyContinue
if ($db) { throw "Publish output contains a database file: $($db.FullName -join ', '). It would overwrite customer data." }
if (Test-Path (Join-Path $publish 'Updater')) { throw "Publish output contains an Updater folder. Remove it from the package." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

# Double-check the zip itself
$entries = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $names = $entries.Entries | ForEach-Object { $_.FullName }
    if ($names | Where-Object { $_ -match '\.db$' })         { throw "ZIP contains a .db file" }
    if ($names | Where-Object { $_ -match '^(?i)updater[/\\]' }) { throw "ZIP contains the Updater folder" }
    if (-not ($names -contains 'Invoice Management System.exe')) { throw "ZIP does not contain the application at its root" }
    $count = $names.Count
} finally { $entries.Dispose() }

$exeVersion = (Get-Item (Join-Path $publish 'Invoice Management System.exe')).VersionInfo.ProductVersion
if ($exeVersion -ne $version) { throw "Exe version $exeVersion does not match csproj version $version" }

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "OK  $zip  ($count files, $size MB)" -ForegroundColor Green
Write-Host "Next: create a release with tag v$version in Dev0ps41/InvoiceManagementSystemUpdates and attach this zip (see RELEASING.md)."
