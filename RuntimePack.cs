using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Linq;

namespace RianConverter {
 public static class RuntimePack {
  static readonly Assembly Owner=typeof(RuntimePack).Assembly;
  static string root;
  public static void Initialize(){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){if(new AssemblyName(e.Name).Name!="PdfSharp")return null;return Assembly.Load(Read("PdfSharp.dll"));};}
  public static byte[] Read(string name){using(var stream=Owner.GetManifestResourceStream("RianConverter.Runtime.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){var entry=zip.GetEntry(name);if(entry==null)throw new IOException("Bundled component is missing: "+name);using(var s=entry.Open())using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}}
  public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
  public static string Tool(string name){
   if(root==null){
    var manifest=XDocument.Parse(System.Text.Encoding.UTF8.GetString(Read("manifest.xml")));
    string id=Hash(Read("manifest.xml")).Substring(0,20);
    string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RianConverter","Runtime",id,Environment.Is64BitProcess?"x64":"x86");
    using(var mutex=new Mutex(false,"Local\\RianConverter.Runtime."+id)){
     bool locked=false;try{try{locked=mutex.WaitOne(60000);}catch(AbandonedMutexException){locked=true;}if(!locked)throw new IOException("Another RianConverter instance is preparing its tools. Try again shortly.");Directory.CreateDirectory(directory);
      foreach(string tool in new[]{"ffmpeg.exe","pdfium.dll"}){
       string entry=tool=="pdfium.dll"?(Environment.Is64BitProcess?"x64/":"x86/")+tool:tool;
       string expected=manifest.Root.Elements("file").Single(e=>(string)e.Attribute("name")==entry).Attribute("sha256").Value;
       string path=Path.Combine(directory,tool);bool valid=false;
       if(File.Exists(path))using(var s=File.OpenRead(path))using(var sha=SHA256.Create())valid=BitConverter.ToString(sha.ComputeHash(s)).Replace("-","")==expected;
       if(!valid){byte[] data=Read(entry);if(Hash(data)!=expected)throw new IOException("Bundled tool integrity check failed.");string temp=path+"."+Guid.NewGuid().ToString("N");File.WriteAllBytes(temp,data);if(File.Exists(path))File.Delete(path);File.Move(temp,path);}
      }
      root=directory;
     }finally{if(locked)mutex.ReleaseMutex();}
    }
   }
   return Path.Combine(root,name);
  }
  public static string Notices(){return System.Text.Encoding.UTF8.GetString(Read("NOTICE.txt"));}
  public static string LicenseTexts(){var text=new System.Text.StringBuilder(Notices());using(var stream=Owner.GetManifestResourceStream("RianConverter.Runtime.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("licenses/"))){text.AppendLine();text.AppendLine("===== "+entry.FullName+" =====");using(var reader=new StreamReader(entry.Open()))text.AppendLine(reader.ReadToEnd());}return text.ToString();}
 }
}
