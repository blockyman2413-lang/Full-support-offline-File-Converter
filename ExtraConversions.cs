using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RianConverter {
 public static partial class Engine {
  static void MediaTool(string arguments){
   string exe=RuntimePack.Tool("ffmpeg.exe");var log=new StringBuilder();var info=new ProcessStartInfo(exe,"-hide_banner -nostdin -loglevel error -threads 1 -filter_threads 1 -filter_complex_threads 1 "+arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
   using(var process=new Process {StartInfo=info}){process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)lock(log){log.AppendLine(e.Data);if(log.Length>16000)log.Remove(0,log.Length-16000);}};process.OutputDataReceived+=delegate{};process.Start();Record(process);process.BeginErrorReadLine();process.BeginOutputReadLine();process.WaitForExit();if(process.ExitCode!=0)throw new IOException("Conversion engine failed ("+process.ExitCode+"): "+log.ToString().Trim());}
  }
  static string LocalInput(string input){return "-protocol_whitelist file,pipe -i "+Quote(input)+" ";}
  static void MediaConvert(string input,string output){
   string target=Formats.Ext(output),options;
   if(Formats.Has(Formats.Subtitles,target)){options="-map 0:s:0 -c:s "+(target==".vtt"?"webvtt":target==".srt"?"srt":target==".ssa"?"ssa":"ass");}
   else if(Formats.Has(Formats.Audio,target)){
    options="-map 0:a:0 -vn -sn -dn ";
    switch(target){case ".mp3":options+="-c:a libmp3lame -b:a 192k";break;case ".wav":options+="-c:a pcm_s16le";break;case ".flac":options+="-c:a flac";break;case ".aac":case ".m4a":options+="-c:a aac -b:a 192k";break;case ".ogg":options+="-c:a libvorbis -q:a 5";break;case ".opus":options+="-c:a libopus -b:a 128k";break;case ".wma":options+="-c:a wmav2 -b:a 192k";break;default:options+="-c:a pcm_s16be";break;}
   }else{
    options="-map 0:v:0 -map 0:a:0? -sn -dn -vf pad=ceil(iw/2)*2:ceil(ih/2)*2 -pix_fmt yuv420p ";
    switch(target){case ".webm":options+="-c:v libvpx-vp9 -deadline good -cpu-used 4 -b:v 2M -c:a libopus -b:a 128k";break;case ".wmv":options+="-c:v wmv2 -b:v 2M -c:a wmav2 -b:a 192k";break;case ".flv":options+="-c:v flv -b:v 2M -c:a libmp3lame -ar 44100 -b:a 128k";break;case ".mpg":case ".mpeg":options+="-c:v mpeg2video -b:v 3M -c:a mp2 -ar 48000 -b:a 192k";break;case ".avi":options+="-c:v mpeg4 -q:v 3 -c:a libmp3lame -b:a 192k";break;default:options+="-c:v mpeg4 -q:v 3 -c:a aac -b:a 192k";if(target==".m4v")options+=" -f mp4";break;}
   }
   MediaTool("-y "+LocalInput(input)+"-map_metadata -1 "+options+" -threads 1"+" "+Quote(output));
   if(!Formats.Has(Formats.Subtitles,target))MediaTool(LocalInput(output)+"-t 1 -f null NUL");
  }
  static Bitmap LoadBitmap(string path){
   if(Formats.Ext(path)==".ico"){
    using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream)){if(reader.ReadUInt16()!=0||reader.ReadUInt16()!=1)throw new IOException("Invalid icon file.");int count=reader.ReadUInt16();if(count<1||count>256)throw new IOException("Invalid icon image count.");int best=-1;uint offset=0,length=0;for(int n=0;n<count;n++){int w=reader.ReadByte();if(w==0)w=256;reader.ReadBytes(7);uint size=reader.ReadUInt32(),start=reader.ReadUInt32();if(w>best){best=w;offset=start;length=size;}}if(offset+length>stream.Length||length>64*1024*1024)throw new IOException("Invalid icon image bounds.");stream.Position=offset;byte[] data=reader.ReadBytes((int)length);if(data.Length>8&&data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71)using(var memory=new MemoryStream(data))using(var image=Image.FromStream(memory))return new Bitmap(image);}
    using(var icon=new Icon(path,new Size(32,32)))return icon.ToBitmap();
   }
   using(var source=Image.FromFile(path)){if((long)source.Width*source.Height>24000000)throw new IOException("Images above 24 megapixels are not supported by this lightweight converter.");return new Bitmap(source);}
  }
  static void ImageConvert(string input,string output){
   string source=Formats.Ext(input),target=Formats.Ext(output);string readable=input;
   if(source==".webp"){readable=Path.Combine(Path.GetDirectoryName(output),"decoded.png");MediaTool("-y "+LocalInput(input)+"-frames:v 1 "+Quote(readable));}
   if(target==".webp"){MediaTool("-y "+LocalInput(readable)+"-frames:v 1 -threads 1 -c:v libwebp -quality 90 "+Quote(output));return;}
   using(var image=LoadBitmap(readable)){
    if(target==".ico"){
     int[] sizes={256,64,48,32,16};var frames=new List<byte[]>();foreach(int size in sizes){using(var frame=new Bitmap(size,size,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(frame)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.Clear(Color.Transparent);float scale=Math.Min((float)size/image.Width,(float)size/image.Height);float w=image.Width*scale,h=image.Height*scale;g.DrawImage(image,(size-w)/2,(size-h)/2,w,h);}using(var memory=new MemoryStream()){frame.Save(memory,ImageFormat.Png);frames.Add(memory.ToArray());}}}
     using(var writer=new BinaryWriter(File.Create(output))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int n=0;n<sizes.Length;n++){writer.Write((byte)(sizes[n]==256?0:sizes[n]));writer.Write((byte)(sizes[n]==256?0:sizes[n]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[n].Length);writer.Write(offset);offset+=frames[n].Length;}foreach(var frame in frames)writer.Write(frame);}return;
    }
    ImageFormat format=target==".png"?ImageFormat.Png:target==".gif"?ImageFormat.Gif:target==".bmp"?ImageFormat.Bmp:target==".tiff"||target==".tif"?ImageFormat.Tiff:ImageFormat.Jpeg;
    if(format==ImageFormat.Jpeg||format==ImageFormat.Bmp){using(var flat=new Bitmap(image.Width,image.Height,PixelFormat.Format24bppRgb)){using(var g=Graphics.FromImage(flat)){g.Clear(Color.White);g.DrawImage(image,0,0,image.Width,image.Height);}flat.Save(output,format);}}else image.Save(output,format);
   }
  }
  static void ZipConvert(string input,string output){
   if(Formats.Ext(output)==".zip"){using(var zip=ZipFile.Open(output,ZipArchiveMode.Create))zip.CreateEntryFromFile(input,Path.GetFileName(input),CompressionLevel.Optimal);return;}
   string folder=output+".files";Directory.CreateDirectory(folder);long total=0;var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string root=Path.GetFullPath(folder)+Path.DirectorySeparatorChar;
   using(var zip=ZipFile.OpenRead(input)){
    if(zip.Entries.Count>10000)throw new IOException("ZIP archives are limited to 10,000 entries.");
    foreach(var entry in zip.Entries){string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar).Replace('\\',Path.DirectorySeparatorChar);int unixType=(entry.ExternalAttributes>>16)&0xF000;
     if(name.Length==0||name.Contains(':')||name.Split(Path.DirectorySeparatorChar).Any(s=>s==".."||s.EndsWith(".")||s.EndsWith(" ")||System.Text.RegularExpressions.Regex.IsMatch(s,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))||Path.IsPathRooted(name)||unixType==0xA000)throw new IOException("The ZIP contains an unsafe path or symbolic link.");
     string target=Path.GetFullPath(Path.Combine(folder,name));if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!paths.Add(target))throw new IOException("The ZIP contains an unsafe or duplicate path.");total=checked(total+entry.Length);if(total>1024L*1024*1024 || entry.Length>256L*1024*1024)throw new IOException("ZIP extraction is limited to 1 GB total and 256 MB per file.");
    }
    foreach(var entry in zip.Entries){string target=Path.GetFullPath(Path.Combine(folder,entry.FullName.Replace('\\','/').Replace('/',Path.DirectorySeparatorChar)));if(entry.Name==""||entry.FullName.EndsWith("/")){Directory.CreateDirectory(target);continue;}Directory.CreateDirectory(Path.GetDirectoryName(target));using(var source=entry.Open())using(var destination=new FileStream(target,FileMode.CreateNew)){byte[] buffer=new byte[65536];long written=0;int read;while((read=source.Read(buffer,0,buffer.Length))>0){written+=read;if(written>entry.Length)throw new IOException("ZIP entry size is invalid.");destination.Write(buffer,0,read);}}}
   }
  }
  static bool TryPortable(string input,string output,string libreOffice,bool preferOffice){
   string source=Formats.Ext(input),target=Formats.Ext(output);if(!Formats.CanConvert(source,target))throw new IOException("This conversion pair is not supported.");
   if(target==".zip"||source==".zip"){ZipConvert(input,output);return true;}
   if(Formats.Has(Formats.Audio,source)||Formats.Has(Formats.Video,source)||Formats.Has(Formats.Subtitles,source)){MediaConvert(input,output);return true;}
   if(Formats.Has(Formats.Data,source)){TableData.Convert(input,output);return true;}
   if(Formats.Has(Formats.Images,source)){
    if(target==".pdf"){string normalized=Path.Combine(Path.GetDirectoryName(output),"picture.png");ImageConvert(input,normalized);PortablePdf.Write(output,"",new[]{normalized});}else ImageConvert(input,output);return true;
   }
   if(source==".pdf"&&(target==".png"||target==".jpg")){string folder=output+".files";Directory.CreateDirectory(folder);using(var pdf=new PortablePdf(input)){if(pdf.Count>2000)throw new IOException("PDF conversion is limited to 2,000 pages.");for(int n=0;n<pdf.Count;n++)using(var bitmap=pdf.Render(n))bitmap.Save(Path.Combine(folder,"Page "+(n+1).ToString("D4")+target),target==".png"?ImageFormat.Png:ImageFormat.Jpeg);}return true;}
   // Existing Office engines remain an optional fidelity enhancement, never a requirement.
   if(target==".pdf"&&((!String.IsNullOrWhiteSpace(libreOffice))||(preferOffice&&(source==".docx"||source==".pptx"))))return false;
   if(source==".pdf"&&target==".docx"&&preferOffice)return false;
   var content=PortableDocuments.Read(input,Path.GetDirectoryName(output));
   if((target==".txt"||target==".rtf"||target==".html"||target==".pptx"&&source!=".pdf")&&String.IsNullOrWhiteSpace(content.Text))throw new IOException("No text was found. Scanned pages require OCR, which is not included.");
   if(target==".docx")PortableDocuments.Word(output,content);
   else if(target==".pptx")PortableDocuments.Slides(output,content,source==".pdf");
   else if(target==".pdf")PortablePdf.Write(output,content.Text,content.Pictures);
   else if(target==".txt")File.WriteAllText(output,content.Text,new UTF8Encoding(true));
   else if(target==".rtf"){using(var box=new RichTextBox()){var handle=box.Handle;box.Text=content.Text;File.WriteAllText(output,box.Rtf,Encoding.ASCII);}}
   else if(target==".html")File.WriteAllText(output,"<!doctype html><html><head><meta charset=\"utf-8\"><title>Converted document</title></head><body><pre>"+System.Net.WebUtility.HtmlEncode(content.Text)+"</pre></body></html>",new UTF8Encoding(false));
   else throw new IOException("Unsupported output format.");return true;
  }
  static bool ValidateExtra(string path,string ext){
   if(!Formats.Has(Formats.Inputs,ext)&&ext!=".html")return false;
   if(Formats.Has(Formats.Images,ext)&&ext!=".webp"){using(var bitmap=LoadBitmap(path)){}return true;}
   if(ext==".zip"){using(var zip=ZipFile.OpenRead(path)){if(zip.Entries.Count==0)throw new IOException("The ZIP archive is empty.");}return true;}
   if(ext==".json"){new System.Web.Script.Serialization.JavaScriptSerializer {MaxJsonLength=32*1024*1024}.DeserializeObject(File.ReadAllText(path));return true;}
   if(ext==".xml"){using(var reader=System.Xml.XmlReader.Create(path,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))while(reader.Read()){}return true;}
   return true;
  }
 }
}
