# RianConverter 2.0 source

C# Windows Forms, .NET Framework 4.8, Any CPU. The release is in `..\Ready`. Standard Windows controls and the supplied logo are retained.

## Build offline

The compact GitHub source archive omits the large engine binaries. After extracting that archive, first run `powershell -ExecutionPolicy Bypass -File .\prepare-runtime.ps1` with internet access to restore the pinned dependencies. Then run the build command below. The full local Source folder already includes these dependencies and can build offline. This development setup does not change the application's offline behavior.

Run `powershell -ExecutionPolicy Bypass -File .\build.ps1`. The Windows framework compiler creates `bin\RianConverter.exe`; no SDK, NuGet restore or network access is required. A Visual Studio project is also included.

`Dependencies\runtime.zip` is the prepacked runtime. `Dependencies\PdfSharp.dll` is only the build reference; the runtime assembly is embedded and loaded from memory. Do not ship a separate DLL beside the final executable.

Explicit architecture builds: `build.ps1 -Platform x86 -OutputDirectory bin-x86` or `-Platform x64 -OutputDirectory bin-x64`. The distributed build is Any CPU without Prefer32Bit. It selects the matching PDF engine; the bundled x86 media engine runs in both application paths.

After verification, copy `bin\RianConverter.exe` to `..\Ready\RianConverter.exe`. The executable, Windows 10+ and .NET Framework 4.8 are sufficient for portable conversion.

## Code

- `Program.cs`: interface, batch queue and settings.
- `Formats.cs`: supported extensions, labels and allowed conversion pairs.
- `Converter.cs`: worker isolation, output publication, timeouts and optional Office integration.
- `ExtraConversions.cs`: images, media, subtitles, ZIP and portable routing.
- `PortableDocuments.cs`: text/picture extraction and DOCX/PPTX package generation.
- `PortablePdf.cs`: PDFium reading/rendering and PDFsharp PDF generation.
- `TableData.cs`: strict CSV/TSV, flat JSON rows and table XML.
- `RuntimePack.cs`: on-demand extraction, integrity checks, architecture selection and licenses.
- `Assets`: supplied logo and blank document templates.

## Tests

- `test-portable.ps1`: portable conversions without Office or external tools.
- `test-portable.ps1 -X86`: the same checks in a 32-bit application process.
- `test.ps1`: Office-created fixtures, reopening outputs in Word/PowerPoint, UI batches and timeout cleanup. This development-only suite requires installed Office and the available test PDF renderer. Close PowerPoint before running it.

The logs are `portable-test-log.txt`, `portable-x86-test-log.txt` and `test-log.txt`. Test cases generate `bin`, `bin-x86`, `portable-results` and `test-results`; these folders can be removed after verification. See `TESTING.md` for the tested scope.

`prepare-runtime.ps1` downloads pinned upstream components for maintenance and calls `pack-runtime.ps1` to replace the runtime bundle. Only these maintenance steps need internet access. Normal builds and the application do not download anything.

## Limits

See `..\Ready\README.md` for the conversion matrix and behavior. The formats are not interchangeable in arbitrary combinations. Document layout is simplified in portable mode. Media uses conservative thread limits for 32-bit compatibility.

ZIP extraction rejects traversal, duplicate paths, device names and links. Limits are 10,000 entries, 1 GB total and 256 MB per file. PDF input is limited to 512 MB, conversion to 2,000 pages, and raster images to 24 megapixels. Text/table input uses UTF-8 with BOM detection; invalid UTF-8 is rejected.

Inputs are copied into private per-job work folders. Results are validated before being moved into place without replacement. Multi-file outputs use new folders. Source files are never modified.

Component attribution, versions and pinned source/build references are in `THIRD-PARTY.txt` and the embedded license viewer. FFmpeg is LGPL, PDFium uses BSD-style and third-party licenses, and PDFsharp is MIT licensed.
