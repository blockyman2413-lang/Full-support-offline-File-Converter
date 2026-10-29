param([string]$StagingDirectory=(Join-Path $PSScriptRoot 'Dependencies\Staging'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$deps=$StagingDirectory
$download=[IO.Compression.ZipFile]::OpenRead((Join-Path $deps 'download.zip'))
$target=Join-Path $PSScriptRoot 'Dependencies\runtime.zip'
if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
$zip=[IO.Compression.ZipFile]::Open($target,[IO.Compression.ZipArchiveMode]::Create)
$manifest=[xml]'<files />'
function Add-Bytes([string]$name,[byte[]]$bytes) {
 $entry=$zip.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
 $stream=$entry.Open();try{$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
 $node=$manifest.CreateElement('file');$node.SetAttribute('name',$name)
 $hash=[Security.Cryptography.SHA256]::Create();try{$node.SetAttribute('sha256',([BitConverter]::ToString($hash.ComputeHash($bytes))).Replace('-',''))}finally{$hash.Dispose()}
 $null=$manifest.DocumentElement.AppendChild($node)
}
try {
 foreach($name in @('ffmpeg.exe','LICENSE.txt')) {
  $entry=$download.Entries | Where-Object {$_.FullName.EndsWith('/'+$name)} | Select-Object -First 1
  $stream=$entry.Open();$memory=[IO.MemoryStream]::new();try{$stream.CopyTo($memory);$destination=if($name -eq 'LICENSE.txt'){'licenses/FFmpeg-LGPL.txt'}else{$name};Add-Bytes $destination $memory.ToArray()}finally{$memory.Dispose();$stream.Dispose()}
 }
 foreach($arch in @('x86','x64')) {
  Add-Bytes "$arch/pdfium.dll" ([IO.File]::ReadAllBytes((Join-Path $deps "$arch/bin/pdfium.dll")))
  foreach($file in Get-ChildItem (Join-Path $deps $arch) -Recurse -File | Where-Object {$_.FullName -match '[\\/]licenses[\\/]' -or $_.Name -eq 'LICENSE'}) {Add-Bytes ("licenses/pdfium-$arch/"+$file.Name) ([IO.File]::ReadAllBytes($file.FullName))}
 }
 Add-Bytes 'PdfSharp.dll' ([IO.File]::ReadAllBytes((Join-Path $deps 'PdfSharp.dll')))
 Add-Bytes 'Blank.docx' ([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Assets/Blank.docx')))
 Add-Bytes 'Blank.pptx' ([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Assets/Blank.pptx')))
 Add-Bytes 'NOTICE.txt' ([IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'THIRD-PARTY.txt')))
 Add-Bytes 'manifest.xml' ([Text.Encoding]::UTF8.GetBytes($manifest.OuterXml))
} finally {$zip.Dispose();$download.Dispose()}
Write-Output ('Bundled runtime: '+(Get-Item -LiteralPath $target).Length+' bytes')
