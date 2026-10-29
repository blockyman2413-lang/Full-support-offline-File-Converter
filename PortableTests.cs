using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RianConverter;
class PortableTests {
 static int passed,failed;static string root;static Settings settings=new Settings {Timeout=180,UseOffice=false,LibreOffice=""};
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static string Convert(string input,string target){return Engine.Run(input,target,Path.Combine(root,"outputs"),settings);}
 static void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e);failed++;}}
 static void Fails(Action action){try{action();}catch{return;}throw new Exception("Expected a failure.");}
 static void Run(string arguments){using(var p=Process.Start(new ProcessStartInfo(RuntimePack.Tool("ffmpeg.exe"),"-hide_banner -loglevel error -nostdin -threads 1 -filter_threads 1 "+arguments.Replace("-c:v","-threads 1 -c:v")){UseShellExecute=false,CreateNoWindow=true})){p.WaitForExit();Check(p.ExitCode==0,"Fixture generation / decoding failed.");}}
 [STAThread] static int Main(){
  RuntimePack.Initialize();root=Path.GetFullPath("portable-results");Directory.CreateDirectory(root);
  try {
   string txt=Path.Combine(root,"sample.txt");File.WriteAllText(txt,"RianConverter verification alpha\r\nA second paragraph with symbols < & > and café.");
   string docx=null,pptx=null,pdf=null;
   Test("TXT to DOCX",delegate{docx=Convert(txt,".docx");using(var z=ZipFile.OpenRead(docx)){using(var s=z.GetEntry("word/document.xml").Open())Check(XDocument.Load(s).ToString().Contains("verification alpha"),"Word text missing.");}});
   Test("TXT to PPTX",delegate{pptx=Convert(txt,".pptx");using(var z=ZipFile.OpenRead(pptx))Check(z.GetEntry("ppt/slides/slide1.xml")!=null,"Slide missing.");});
   Test("TXT to PDF",delegate{pdf=Convert(txt,".pdf");using(var d=new PortablePdf(pdf)){Check(d.Count>0,"PDF pages missing.");Check(d.Text(0).Contains("verification alpha")&&d.Text(0).Contains("café"),"PDF text missing.");using(var b=d.Render(0))b.Save(Path.Combine(root,"pdf-preview.png"));}});
   Test("TXT to RTF to TXT",delegate{string rtf=Convert(txt,".rtf");Check(File.ReadAllText(Convert(rtf,".txt")).Contains("verification alpha"),"RTF roundtrip failed.");});
   Test("TXT to HTML escapes markup",delegate{string html=File.ReadAllText(Convert(txt,".html"));Check(html.Contains("&lt;")&&html.Contains("&amp;"),"HTML was not escaped.");});
   if(docx!=null&&pptx!=null&&pdf!=null){
    foreach(var pair in new[]{new[]{docx,".pdf"},new[]{docx,".pptx"},new[]{pptx,".pdf"},new[]{pptx,".docx"},new[]{pdf,".docx"},new[]{pdf,".pptx"}}){var job=pair;Test(Formats.Ext(job[0])+" to "+job[1]+" without Office",delegate{string result=Convert(job[0],job[1]);Engine.Validate(result);if(job[1]==".pdf")using(var d=new PortablePdf(result))Check(d.Text(0).Contains("verification alpha"),"Expected text missing.");});}
    Test("PDF to page images",delegate{string folder=Convert(pdf,".png");Check(Directory.GetFiles(folder,"*.png").Length==1,"Incorrect page count.");});
   }
   string png=Path.Combine(root,"picture.png");using(var image=new Bitmap(80,60,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(image)){g.Clear(Color.Transparent);g.FillRectangle(Brushes.CornflowerBlue,5,5,60,40);}image.Save(png,ImageFormat.Png);}
   foreach(string ext in new[]{".jpg",".bmp",".gif",".tiff",".ico",".webp",".pdf"}){string target=ext;Test("PNG to "+target,delegate{string output=Convert(png,target);Engine.Validate(output);if(target==".webp")Check(File.Exists(Convert(output,".png")),"WebP decoding failed.");});}
   Test("JPEG transparency becomes white",delegate{using(var image=new Bitmap(Convert(png,".jpg"))){Color pixel=image.GetPixel(0,0);Check(pixel.R>230&&pixel.G>230&&pixel.B>230,"Transparent pixels were not flattened onto white.");}});
   Test("Multipage PDF to images and slides",delegate{string path=Path.Combine(root,"multipage.pdf");PortablePdf.Write(path,String.Join("\n",Enumerable.Range(1,120).Select(n=>"Line "+n)),new string[0]);int count;using(var document=new PortablePdf(path))count=document.Count;Check(count>1,"Multipage fixture has only one page.");string folder=Convert(path,".png");Check(Directory.GetFiles(folder,"*.png").Length==count,"PDF page count changed.");using(var zip=ZipFile.OpenRead(Convert(path,".pptx")))using(var stream=zip.GetEntry("ppt/presentation.xml").Open())Check(XDocument.Load(stream).Descendants().Count(e=>e.Name.LocalName=="sldId")==count,"Presentation page count changed.");});
   Test("Scanned PDF text rejection and Word image preservation",delegate{string path=Convert(png,".pdf");Fails(()=>Convert(path,".txt"));Fails(()=>Convert(path,".rtf"));Fails(()=>Convert(path,".html"));using(var zip=ZipFile.OpenRead(Convert(path,".docx")))Check(zip.Entries.Any(e=>e.FullName.StartsWith("word/media/rian-")),"Scanned PDF page image was lost.");});
   string csv=Path.Combine(root,"table.csv");File.WriteAllText(csv,"Name,Note\r\nAlice,\"Hello, world\"\r\nBob,\"Line 1\nLine 2\"\r\nZoë,\"He said \"\"yes\"\"\"\r\n",new UTF8Encoding(true));
   foreach(string ext in new[]{".tsv",".json",".xml"}){string target=ext;Test("CSV to "+target+" and back",delegate{string converted=Convert(csv,target);string roundtrip=Convert(converted,".csv");string contents=File.ReadAllText(roundtrip);Check(contents.Contains("Hello, world")&&contents.Contains("Line 1\nLine 2")&&contents.Contains("Zoë"),"Table cells changed.");});}
   Test("Reject nested JSON",delegate{string f=Path.Combine(root,"nested.json");File.WriteAllText(f,"[{\"value\":{\"nested\":1}}]");Fails(()=>Convert(f,".csv"));});
   Test("Reject malformed CSV",delegate{string f=Path.Combine(root,"invalid.csv");File.WriteAllText(f,"A,B\n\"unclosed,B");Fails(()=>Convert(f,".json"));});
   Test("ZIP package and extract",delegate{string archive=Convert(txt,".zip");string folder=Convert(archive,".extract");Check(File.ReadAllText(Path.Combine(folder,"sample.txt"))==File.ReadAllText(txt),"ZIP contents changed.");});
   Test("Reject ZIP traversal",delegate{string archive=Path.Combine(root,"unsafe-"+Guid.NewGuid().ToString("N")+".zip");using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("../escape.txt").Open()))writer.Write("escape");Fails(()=>Convert(archive,".extract"));Check(!File.Exists(Path.Combine(root,"escape.txt")),"ZIP escaped its output folder.");});
   string wav=Path.Combine(root,"tone.wav");Run("-y -f lavfi -i sine=frequency=440:duration=0.5 "+Engine.Quote(wav));
   foreach(string ext in new[]{".mp3",".flac",".aac",".m4a",".ogg",".opus",".wma",".aiff"}){string target=ext;Test("WAV to "+target+" and decode",delegate{string result=Convert(wav,target);string back=Convert(result,".wav");Check(new FileInfo(back).Length>1000,"Audio is empty.");});}
   string video=Path.Combine(root,"video.mp4");Run("-y -f lavfi -i color=c=blue:s=160x120:r=25:d=0.5 -f lavfi -i sine=frequency=440:duration=0.5 -shortest -c:v mpeg4 -c:a aac "+Engine.Quote(video));
   foreach(string ext in new[]{".mkv",".avi",".mov",".webm",".wmv",".flv",".m4v",".3gp"}){string target=ext;Test("MP4 to "+target+" and decode",delegate{string result=Convert(video,target);Run("-i "+Engine.Quote(result)+" -t 1 -f null NUL");});}
   Test("Video to audio",delegate{string result=Convert(video,".mp3");Check(new FileInfo(result).Length>100,"Audio extraction failed.");});
   Test("Matroska to MP4",delegate{string result=Convert(Convert(video,".mkv"),".mp4");Run("-i "+Engine.Quote(result)+" -t 1 -f null NUL");});
   string srt=Path.Combine(root,"captions.srt");File.WriteAllText(srt,"1\r\n00:00:00,000 --> 00:00:01,000\r\nRianConverter verification alpha\r\n");
   foreach(string ext in new[]{".vtt",".ass",".ssa"}){string target=ext;Test("SRT to "+target+" and back",delegate{string result=Convert(srt,target);Check(File.ReadAllText(Convert(result,".srt")).Contains("verification alpha"),"Subtitle text missing.");});}
   Test("Unsupported pairs rejected",delegate{Fails(()=>Convert(wav,".docx"));Fails(()=>Convert(png,".mp4"));Fails(()=>Convert(txt,".txt"));});
   Test("Duplicate names and source preservation",delegate{string before=RuntimePack.Hash(File.ReadAllBytes(txt));string a=Convert(txt,".docx"),b=Convert(txt,".docx");Check(a!=b,"Output was overwritten.");Check(before==RuntimePack.Hash(File.ReadAllBytes(txt)),"Source changed.");});
   Console.WriteLine("RESULT: "+passed+" passed, "+failed+" failed");return failed==0?0:1;
  }catch(Exception e){Console.WriteLine("FAIL test setup: "+e);return 1;}
 }
}
