[CmdletBinding()]
param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$outputDir = Join-Path $projectDir 'dist'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework C# compiler was not found.' }
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sourceFiles = @('Core.cs', 'FaceRenderer.cs', 'Native.cs', 'SaverWindow.cs', 'Program.cs') | ForEach-Object { Join-Path $projectDir $_ }
$commonArgs = @('/nologo', '/optimize+', '/warn:4', '/warnaserror+', '/platform:anycpu', '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', "/win32manifest:$(Join-Path $projectDir 'app.manifest')")
& $compilerPath @commonArgs '/target:winexe' "/out:$(Join-Path $outputDir 'StackchanSaver.exe')" @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item -LiteralPath (Join-Path $outputDir 'StackchanSaver.exe') -Destination (Join-Path $outputDir 'StackchanSaver.scr')
foreach ($name in @('StackchanSaver.exe', 'StackchanSaver.scr')) {
    Copy-Item -LiteralPath (Join-Path $projectDir 'StackchanSaver.exe.config') -Destination (Join-Path $outputDir "$name.config")
}
Copy-Item -LiteralPath (Join-Path $projectDir 'README.md') -Destination $outputDir
Copy-Item -LiteralPath (Join-Path $projectDir '..\LICENSE') -Destination $outputDir
Copy-Item -LiteralPath (Join-Path $projectDir '..\THIRD-PARTY-NOTICES.md') -Destination $outputDir
Write-Host "Built: $outputDir\StackchanSaver.scr"
if ($Test -or $Package) {
    $testDir = Join-Path $projectDir 'test-results'
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    & $compilerPath @commonArgs '/target:exe' '/main:Stackchan.Tests' "/out:$(Join-Path $testDir 'StackchanTests.exe')" @sourceFiles (Join-Path $projectDir 'Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & (Join-Path $testDir 'StackchanTests.exe') $testDir (Join-Path $outputDir 'StackchanSaver.scr')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. See test-results\report.txt.' }
    $verificationDir = Join-Path $outputDir 'verification'
    New-Item -ItemType Directory -Path $verificationDir -Force | Out-Null
    foreach ($name in @('report.txt', 'faces.png', 'window-after-resume.png')) {
        Copy-Item -LiteralPath (Join-Path $testDir $name) -Destination $verificationDir
    }
}
if ($Package) {
    $zipPath = Join-Path $projectDir 'StackchanSaver-Windows.zip'
    Compress-Archive -Path (Join-Path $outputDir '*') -DestinationPath $zipPath -Force
    Write-Host "Packaged: $zipPath"
}
