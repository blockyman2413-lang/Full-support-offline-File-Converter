param([string]$StagingDirectory=(Join-Path $PSScriptRoot 'Dependencies\Staging'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $StagingDirectory | Out-Null
$headers=@{'User-Agent'='RianConverter-build'}
$release=Invoke-RestMethod -Uri 'https://api.github.com/repos/defisym/FFmpeg-Builds-Win32/releases/tags/autobuild-2026-10-28-20-31' -Headers $headers
$asset=$release.assets | Where-Object {$_.name -match 'n8\.1.*win32-lgpl-8\.1\.zip$' -and $_.name -notmatch 'shared'} | Select-Object -First 1
if(-not $asset){throw 'Pinned FFmpeg release is unavailable. Use the supplied runtime.zip or deliberately update the dependency pin.'}
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile (Join-Path $StagingDirectory 'download.zip')
$runtime=Join-Path $PSScriptRoot 'Dependencies\runtime.zip'
if(Test-Path -LiteralPath $runtime){
 $existing=[IO.Compression.ZipFile]::OpenRead($runtime)
 try {$reader=[IO.StreamReader]::new($existing.GetEntry('manifest.xml').Open());try{[xml]$manifest=$reader.ReadToEnd()}finally{$reader.Dispose()}}finally{$existing.Dispose()}
}else{
 [xml]$manifest=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Dependencies\manifest.xml'))
}
function Verify-Bytes([string]$name,[byte[]]$bytes){$expected=($manifest.files.file | Where-Object {$_.name -eq $name}).sha256;$sha=[Security.Cryptography.SHA256]::Create();try{$actual=([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','')}finally{$sha.Dispose()};if($actual -ne $expected){throw ('Pinned component checksum mismatch: '+$name)}}
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $StagingDirectory 'download.zip'))
try {$entry=$archive.Entries | Where-Object {$_.FullName.EndsWith('/ffmpeg.exe')} | Select-Object -First 1;$stream=$entry.Open();$memory=[IO.MemoryStream]::new();try{$stream.CopyTo($memory);Verify-Bytes 'ffmpeg.exe' $memory.ToArray()}finally{$stream.Dispose();$memory.Dispose()}}finally{$archive.Dispose()}
foreach($arch in @('x86','x64')){
 $package=Join-Path $StagingDirectory ('pdfium-'+$arch+'.tgz')
 Invoke-WebRequest -Uri ('https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/8125/pdfium-win-'+$arch+'.tgz') -OutFile $package
 $directory=Join-Path $StagingDirectory $arch;New-Item -ItemType Directory -Force -Path $directory | Out-Null
 & tar -xzf $package -C $directory
 if($LASTEXITCODE -ne 0){throw 'PDFium extraction failed.'}
 Verify-Bytes "$arch/pdfium.dll" ([IO.File]::ReadAllBytes((Join-Path $directory 'bin\pdfium.dll')))
}
$package=Join-Path $StagingDirectory 'pdfsharp.zip'
Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/pdfsharp/1.50.5147/pdfsharp.1.50.5147.nupkg' -OutFile $package
$archive=[IO.Compression.ZipFile]::OpenRead($package)
try {$stream=$archive.GetEntry('lib/net20/PdfSharp.dll').Open();$memory=[IO.MemoryStream]::new();try{$stream.CopyTo($memory);$bytes=$memory.ToArray();Verify-Bytes 'PdfSharp.dll' $bytes;[IO.File]::WriteAllBytes((Join-Path $StagingDirectory 'PdfSharp.dll'),$bytes);[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'Dependencies\PdfSharp.dll'),$bytes)}finally{$stream.Dispose();$memory.Dispose()}}finally{$archive.Dispose()}
& (Join-Path $PSScriptRoot 'pack-runtime.ps1') -StagingDirectory $StagingDirectory
