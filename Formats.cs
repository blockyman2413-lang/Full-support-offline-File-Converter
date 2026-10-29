using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RianConverter {
 public sealed class OutputFormat {
  public string Extension, Name;
  public OutputFormat(string extension,string name){Extension=extension;Name=name;}
  public override string ToString(){return Extension==".extract"?Name:Name+" ("+Extension+")";}
 }
 public static class Formats {
  public static readonly string[] Images={".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff",".ico",".webp"};
  public static readonly string[] Audio={".mp3",".wav",".flac",".aac",".m4a",".ogg",".opus",".wma",".aiff",".aif"};
  public static readonly string[] Video={".mp4",".mkv",".avi",".mov",".webm",".wmv",".flv",".m4v",".3gp"};
  public static readonly string[] Subtitles={".srt",".vtt",".ass",".ssa"};
  public static readonly string[] Data={".csv",".tsv",".json",".xml"};
  public static readonly string[] Documents={".docx",".pptx",".pdf",".txt",".rtf"};
  public static readonly string[] Inputs=Images.Concat(Audio).Concat(Video).Concat(Subtitles).Concat(Data).Concat(Documents).Concat(new[]{".zip"}).ToArray();
  public static readonly OutputFormat[] Outputs={
   new OutputFormat(".docx","Word"),new OutputFormat(".pptx","PowerPoint"),new OutputFormat(".pdf","PDF"),new OutputFormat(".txt","Plain text"),new OutputFormat(".rtf","Rich text"),new OutputFormat(".html","HTML"),
   new OutputFormat(".png","PNG image"),new OutputFormat(".jpg","JPEG image"),new OutputFormat(".bmp","Bitmap image"),new OutputFormat(".gif","GIF image"),new OutputFormat(".tiff","TIFF image"),new OutputFormat(".ico","Windows icon"),new OutputFormat(".webp","WebP image"),
   new OutputFormat(".mp3","MP3 audio"),new OutputFormat(".wav","WAV audio"),new OutputFormat(".flac","FLAC audio"),new OutputFormat(".aac","AAC audio"),new OutputFormat(".m4a","M4A audio"),new OutputFormat(".ogg","Ogg Vorbis audio"),new OutputFormat(".opus","Opus audio"),new OutputFormat(".wma","Windows Media audio"),new OutputFormat(".aiff","AIFF audio"),
   new OutputFormat(".mp4","MP4 video"),new OutputFormat(".mkv","Matroska video"),new OutputFormat(".avi","AVI video"),new OutputFormat(".mov","QuickTime video"),new OutputFormat(".webm","WebM video"),new OutputFormat(".wmv","Windows Media video"),new OutputFormat(".flv","Flash video"),new OutputFormat(".m4v","M4V video"),new OutputFormat(".3gp","3GP video"),
   new OutputFormat(".srt","SubRip subtitles"),new OutputFormat(".vtt","WebVTT subtitles"),new OutputFormat(".ass","ASS subtitles"),new OutputFormat(".ssa","SSA subtitles"),
   new OutputFormat(".csv","CSV table"),new OutputFormat(".tsv","TSV table"),new OutputFormat(".json","JSON table"),new OutputFormat(".xml","XML table"),new OutputFormat(".zip","ZIP archive"),new OutputFormat(".extract","Extract ZIP to folder")
  };
  public static bool Has(string[] group,string ext){return group.Contains(ext,StringComparer.OrdinalIgnoreCase);}
  public static string Ext(string path){return Path.GetExtension(path).ToLowerInvariant();}
  public static bool CanConvert(string source,string target){
   if(source==target || !Has(Inputs,source))return false;
   if(target==".zip")return source!=".zip";
   if(source==".zip")return target==".extract";
   if(Has(Audio,source))return Has(Audio,target);
   if(Has(Video,source))return Has(Video,target)||Has(Audio,target);
   if(Has(Subtitles,source))return Has(Subtitles,target);
   if(Has(Images,source))return Has(Images,target)||target==".pdf";
   if(Has(Data,source))return Has(Data,target);
   if(Has(Documents,source))return Has(Documents,target)||target==".html"||(source==".pdf" && (target==".png"||target==".jpg"));
   return false;
  }
  public static string Description(string target){
   if(target==".zip")return "Creates an archive containing each original file. This packages the file without changing its contents.";
   if(target==".extract")return "Extracts ZIP contents into a new folder. Unsafe paths, links and oversized archives are rejected.";
   if(Has(Images,target))return "Images use the first frame/page. PDF pages are saved in a separate folder. JPEG/BMP flatten transparency onto white.";
   if(Has(Audio,target))return "Audio is converted locally; video input extracts its first audio track. Lossy formats can reduce quality.";
   if(Has(Video,target))return "Converts the first video track and, when available, the first audio track. Extra tracks and metadata are not retained.";
   if(Has(Data,target))return "Table data only: CSV/TSV, flat JSON rows and RianConverter table XML. Values are stored as text; nested data is rejected.";
   if(Has(Subtitles,target))return "Converts text subtitles. Styling may change. This does not transcribe audio or perform OCR.";
   if(target==".docx")return "Document layout is simplified. PDF input becomes extracted text plus page snapshots; editable PDF layout is not reconstructed.";
   if(target==".pptx")return "Word/text input creates simple text slides. Each PDF page becomes an image on one slide; page text is not editable.";
   if(target==".txt"||target==".rtf"||target==".html")return "Extracts text only. Images and original styling are omitted. Scanned pages need OCR, which is not included.";
   return "Portable PDF export uses text and pictures with simplified layout. Enable optional installed Office for higher fidelity.";
  }
 }
}
