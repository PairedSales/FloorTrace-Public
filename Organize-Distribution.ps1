# PowerShell script to organize distribution package
param(
    [string]$PublishDir = "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish",
    [string]$DistDir = "dist"
)

Write-Host "Organizing FloorTrace distribution..." -ForegroundColor Cyan

# Clean dist directory if it exists
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}

# Create dist directory structure
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
New-Item -ItemType Directory -Path "$DistDir\libs" -Force | Out-Null

# Copy exe and config files to root
Copy-Item -Path "$PublishDir\FloorTrace.exe" -Destination $DistDir
Copy-Item -Path "$PublishDir\FloorTrace.dll" -Destination "$DistDir\libs"
Copy-Item -Path "$PublishDir\FloorTrace.runtimeconfig.json" -Destination $DistDir
Copy-Item -Path "$PublishDir\FloorTrace.deps.json" -Destination $DistDir
Copy-Item -Path "$PublishDir\appsettings.json" -Destination $DistDir

# Copy all DLL files to libs subdirectory (excluding FloorTrace.dll which we already copied)
Get-ChildItem -Path $PublishDir -Filter "*.dll" | Where-Object { $_.Name -ne "FloorTrace.dll" } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination "$DistDir\libs"
}

# Copy additional distribution files
Copy-Item -Path "README.md" -Destination $DistDir -ErrorAction SilentlyContinue
Copy-Item -Path "LICENSE" -Destination $DistDir -ErrorAction SilentlyContinue
Copy-Item -Path "INSTALLATION.txt" -Destination $DistDir -ErrorAction SilentlyContinue
Copy-Item -Path "ExampleFloorplan.png" -Destination $DistDir -ErrorAction SilentlyContinue

Write-Host "`nDistribution organized successfully!" -ForegroundColor Green
Write-Host "`nContents:" -ForegroundColor Yellow
Write-Host "- Root directory: exe, config files, documentation"
Write-Host "- libs directory: all DLL dependencies"

# Show directory sizes
$rootFiles = Get-ChildItem -Path $DistDir -File | Measure-Object -Property Length -Sum
$libsFiles = Get-ChildItem -Path "$DistDir\libs" -File | Measure-Object -Property Length -Sum
$totalSize = ($rootFiles.Sum + $libsFiles.Sum) / 1MB

Write-Host "`nSize breakdown:" -ForegroundColor Yellow
Write-Host "- Root files: $([math]::Round($rootFiles.Sum / 1MB, 2)) MB"
Write-Host "- libs directory: $([math]::Round($libsFiles.Sum / 1MB, 2)) MB"
Write-Host "- Total: $([math]::Round($totalSize, 2)) MB"

# Create ZIP file
$zipName = "FloorTrace-v1.0.0-alpha-win-x64.zip"
if (Test-Path $zipName) {
    Remove-Item -Path $zipName -Force
}

Write-Host "`nCreating distribution ZIP..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($DistDir, $zipName)

$zipSize = (Get-Item $zipName).Length / 1MB
Write-Host "Created: $zipName ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green

Write-Host "`nDistribution package is ready!" -ForegroundColor Green





