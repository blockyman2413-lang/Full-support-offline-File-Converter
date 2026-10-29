param([switch]$X86)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$platform=if($X86){'x86'}else{'anycpu'}
$binaryFolder=if($X86){'bin-x86'}else{'bin'}
& .\build.ps1 -Platform $platform -OutputDirectory $binaryFolder
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:$platform /out:"$PSScriptRoot\$binaryFolder\PortableTests.exe" /reference:"$PSScriptRoot\$binaryFolder\RianConverter.exe" /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Xml.Linq.dll "$PSScriptRoot\PortableTests.cs"
if($LASTEXITCODE -ne 0){throw 'Portable test build failed.'}
$log=if($X86){'portable-x86-test-log.txt'}else{'portable-test-log.txt'}
& "$PSScriptRoot\$binaryFolder\PortableTests.exe" | Tee-Object -FilePath (Join-Path $PSScriptRoot $log)
if($LASTEXITCODE -ne 0){throw 'Portable tests failed.'}
