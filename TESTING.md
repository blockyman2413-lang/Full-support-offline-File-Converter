# Verification — RianConverter 2.0

Verified on 29 October 2026 on this 64-bit Windows machine.

## Results

- Portable suite, Any CPU / 64-bit process: **52 passed, 0 failed**.
- Portable suite, x86 process: **52 passed, 0 failed**.
- Office interoperability, UI batches, original-file protection, output collision checks and timeout cleanup: **passed**.

The portable suites use only the bundled engines and framework libraries. They do not require Office, Poppler, an installed FFmpeg, API keys or a network connection. A fresh embedded-runtime cache was also exercised.

The x86 checks run a genuine 32-bit executable and use the 32-bit PDF engine under Windows' 32-bit compatibility layer. They are not a test of a separately installed 32-bit Windows operating system. The shipped executable is Any CPU and selects the native PDF engine matching its application process.

## Covered behavior

- TXT to DOCX/PPTX/PDF/RTF/HTML; RTF text roundtrip; HTML escaping.
- All six original DOCX/PPTX/PDF directions in portable mode, with valid output packages and expected content.
- DOCX/PPTX outputs reopened in installed Word and PowerPoint during the separate interoperability suite. Embedded slide pictures are retained in Word output.
- PDF text extraction, Unicode text including café, page rendering, multipage image output and one slide per PDF page.
- Scanned-page snapshots retained in DOCX. Text-only outputs reject scans instead of publishing empty documents.
- PNG to JPEG/BMP/GIF/TIFF/ICO/WebP/PDF, WebP decoding and white transparency flattening for JPEG.
- WAV to MP3/FLAC/AAC/M4A/OGG/Opus/WMA/AIFF and back to decoded WAV.
- MP4 to MKV/AVI/MOV/WebM/WMV/FLV/M4V/3GP with output decoding; MKV to MP4; video-to-audio extraction.
- SRT to VTT/ASS/SSA and back, preserving caption text.
- CSV to TSV/JSON/XML and back, preserving quoted delimiters, multiline cells and Unicode. Malformed CSV and nested JSON are rejected.
- ZIP creation/extraction preserves payload bytes and original filenames. ZIP traversal is rejected.
- Unsupported pairs and same-format jobs are rejected. Duplicate outputs are renamed and originals remain unchanged.
- A real WinForms document batch completes valid jobs before and after a corrupt document, with progress 3/3 and a responsive UI timer.
- A mixed image/audio UI batch filters impossible outputs, marks the incompatible image Unsupported, converts audio, and reports progress 1/1.
- A deliberately stalled optional engine times out without publishing output; its worker and child process are cleaned up.
- Missing inputs, corrupt DOCX files, invalid output folders and a missing configured optional engine fail clearly.
- Converter, Settings, logo and mixed-media screens were rendered and visually inspected. PDF text output was also visually inspected.

MPEG/MPG was excluded from the supported list after its output path crashed in the bundled x86 engine. No passing claim is made for that route.

## Evidence and limits

Logs: `portable-test-log.txt`, `portable-x86-test-log.txt`, `test-log.txt`. Scripts regenerate fixtures and screenshots. Generated test files were removed after verification to keep Source and Ready clean.

These are representative integration checks, not an exhaustive test of every source/target pair or every codec inside a container. Not comprehensively tested: clean Windows installations, a native 32-bit OS, very large/4K media, unusual fonts/languages, damaged or encrypted inputs beyond the tested cases, deliberately injected permission failures, or optional LibreOffice integration. Portable document output deliberately simplifies layout; OCR and full editable PDF layout reconstruction are not implemented.

Native dependency inspection found only Windows system/UCRT imports for the bundled PDF and media binaries. No separately installed VC++ redistributable was identified as a dependency. Windows 10+ and .NET Framework 4.8 remain required.
Rename verification: RianConverter window title, supplied icon, embedded component branding and offline TXT/PDF/PPTX conversions passed after rebuilding. Existing RianFile settings are read as a fallback; new settings use the RianConverter application-data folder.
