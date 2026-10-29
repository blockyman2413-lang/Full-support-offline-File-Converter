using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

namespace RianConverter {
 public sealed class DocumentContent {
  public string Text="";
  public readonly List<string> Pictures=new List<string>();
 }
 public static class PortableDocuments {
  static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main",A="http://schemas.openxmlformats.org/drawingml/2006/main",P="http://schemas.openxmlformats.org/presentationml/2006/main",R="http://schemas.openxmlformats.org/officeDocument/2006/relationships",Rel="http://schemas.openxmlformats.org/package/2006/relationships",Ct="http://schemas.openxmlformats.org/package/2006/content-types";
  static XDocument ReadXml(ZipArchive zip,string part){var entry=zip.GetEntry(part);if(entry==null||entry.Length>32*1024*1024)throw new IOException("Missing or oversized document part: "+part);using(var stream=entry.Open())return XDocument.Load(stream);}
  static void PutXml(ZipArchive zip,string part,XDocument xml){var old=zip.GetEntry(part);if(old!=null)old.Delete();using(var stream=zip.CreateEntry(part).Open())xml.Save(stream);}
  static void PutFile(ZipArchive zip,string part,string path){var old=zip.GetEntry(part);if(old!=null)old.Delete();using(var stream=zip.CreateEntry(part).Open())using(var input=File.OpenRead(path))input.CopyTo(stream);}
  static string Resolve(string part,string target){var pieces=new List<string>(part.Split('/').Take(part.Split('/').Length-1));if(target.StartsWith("/"))pieces.Clear();foreach(var piece in target.Split('/')){if(piece==""||piece==".")continue;if(piece==".."){if(pieces.Count==0)throw new IOException("Invalid document relationship.");pieces.RemoveAt(pieces.Count-1);}else pieces.Add(piece);}return String.Join("/",pieces);}
  static void Pictures(ZipArchive zip,string part,XDocument xml,string folder,DocumentContent content){
   string relPath=Path.GetDirectoryName(part).Replace('\\','/')+"/_rels/"+Path.GetFileName(part)+".rels";if(zip.GetEntry(relPath)==null)return;var rels=ReadXml(zip,relPath);
   foreach(string id in xml.Descendants(A+"blip").Select(x=>(string)x.Attribute(R+"embed")).Where(x=>x!=null).Distinct()){
    var rel=rels.Root.Elements().FirstOrDefault(x=>(string)x.Attribute("Id")==id);if(rel==null||(string)rel.Attribute("TargetMode")=="External")continue;
    string target=Resolve(part,(string)rel.Attribute("Target"));var entry=zip.GetEntry(target);if(entry==null||entry.Length>64*1024*1024)throw new IOException("Missing or oversized embedded image.");
    string raw=Path.Combine(folder,"embedded-"+Guid.NewGuid().ToString("N")+Path.GetExtension(target));using(var input=entry.Open())using(var output=File.Create(raw))input.CopyTo(output);
    string normalized=Path.ChangeExtension(raw,".png");using(var image=Image.FromFile(raw)){if((long)image.Width*image.Height>24000000)throw new IOException("Embedded image is too large.");if(raw!=normalized)image.Save(normalized,ImageFormat.Png);}content.Pictures.Add(normalized);
   }
  }
  public static DocumentContent Read(string input,string folder){
   string ext=Formats.Ext(input);var content=new DocumentContent();
   if(ext==".txt"){content.Text=File.ReadAllText(input,new UTF8Encoding(false,true));return content;}
   if(ext==".rtf"){using(var box=new RichTextBox()){var handle=box.Handle;box.LoadFile(input,RichTextBoxStreamType.RichText);content.Text=box.Text;}return content;}
   if(ext==".pdf"){using(var pdf=new PortablePdf(input)){if(pdf.Count>2000)throw new IOException("PDF conversion is limited to 2,000 pages per job.");var texts=new List<string>();for(int n=0;n<pdf.Count;n++){texts.Add(pdf.Text(n));string image=Path.Combine(folder,"pdf-page-"+(n+1)+".png");using(var bitmap=pdf.Render(n))bitmap.Save(image,ImageFormat.Png);content.Pictures.Add(image);}content.Text=String.Join("\r\n\r\n",texts);}return content;}
   using(var zip=ZipFile.OpenRead(input)){
    if(ext==".docx"){var xml=ReadXml(zip,"word/document.xml");content.Text=String.Join("\r\n",xml.Descendants(W+"p").Select(p=>String.Concat(p.Descendants().Where(x=>x.Name==W+"t"||x.Name==W+"tab"||x.Name==W+"br").Select(x=>x.Name==W+"tab"?"\t":x.Name==W+"br"?"\n":x.Value))));Pictures(zip,"word/document.xml",xml,folder,content);}
    else {var pres=ReadXml(zip,"ppt/presentation.xml");var rels=ReadXml(zip,"ppt/_rels/presentation.xml.rels");var text=new StringBuilder();int n=0;foreach(var slide in pres.Descendants(P+"sldId")){string id=(string)slide.Attribute(R+"id");var rel=rels.Root.Elements().Single(x=>(string)x.Attribute("Id")==id);string path=Resolve("ppt/presentation.xml",(string)rel.Attribute("Target"));var xml=ReadXml(zip,path);text.AppendLine("Slide "+(++n));foreach(var paragraph in xml.Descendants(A+"p"))text.AppendLine(String.Concat(paragraph.Descendants(A+"t").Select(x=>x.Value)));Pictures(zip,path,xml,folder,content);}content.Text=text.ToString();}
   }
   return content;
  }
  static void ImageType(XDocument types){if(!types.Root.Elements(Ct+"Default").Any(e=>(string)e.Attribute("Extension")=="png"))types.Root.Add(new XElement(Ct+"Default",new XAttribute("Extension","png"),new XAttribute("ContentType","image/png")));}
  public static void Word(string output,DocumentContent content){
   File.WriteAllBytes(output,RuntimePack.Read("Blank.docx"));using(var zip=ZipFile.Open(output,ZipArchiveMode.Update)){
    var xml=ReadXml(zip,"word/document.xml");var body=xml.Root.Element(W+"body");var section=body.Element(W+"sectPr");if(section!=null)section.Remove();body.RemoveNodes();
    foreach(string line in content.Text.Replace("\r","").Split('\n'))body.Add(new XElement(W+"p",new XElement(W+"r",new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),line))));
    var rels=ReadXml(zip,"word/_rels/document.xml.rels");var types=ReadXml(zip,"[Content_Types].xml");ImageType(types);
    XNamespace wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing",pic="http://schemas.openxmlformats.org/drawingml/2006/picture";int index=0;
    foreach(string path in content.Pictures){index++;string id="rIdRianImage"+index;long cx,cy;using(var image=Image.FromFile(path)){double scale=Math.Min(5943600.0/image.Width,8000000.0/image.Height);cx=(long)(image.Width*scale);cy=(long)(image.Height*scale);}
     string part="word/media/rian-"+index+".png";PutFile(zip,part,path);rels.Root.Add(new XElement(Rel+"Relationship",new XAttribute("Id",id),new XAttribute("Type",R.NamespaceName+"/image"),new XAttribute("Target","media/rian-"+index+".png")));
     var picture=new XElement(pic+"pic",new XElement(pic+"nvPicPr",new XElement(pic+"cNvPr",new XAttribute("id",index),new XAttribute("name","Picture "+index)),new XElement(pic+"cNvPicPr")),new XElement(pic+"blipFill",new XElement(A+"blip",new XAttribute(R+"embed",id)),new XElement(A+"stretch",new XElement(A+"fillRect"))),new XElement(pic+"spPr",new XElement(A+"xfrm",new XElement(A+"off",new XAttribute("x",0),new XAttribute("y",0)),new XElement(A+"ext",new XAttribute("cx",cx),new XAttribute("cy",cy))),new XElement(A+"prstGeom",new XAttribute("prst","rect"),new XElement(A+"avLst"))));
     body.Add(new XElement(W+"p",new XElement(W+"r",new XElement(W+"drawing",new XElement(wp+"inline",new XElement(wp+"extent",new XAttribute("cx",cx),new XAttribute("cy",cy)),new XElement(wp+"docPr",new XAttribute("id",index),new XAttribute("name","Picture "+index)),new XElement(A+"graphic",new XElement(A+"graphicData",new XAttribute("uri",pic.NamespaceName),picture)))))));
    }
    if(section!=null)body.Add(section);PutXml(zip,"word/document.xml",xml);PutXml(zip,"word/_rels/document.xml.rels",rels);PutXml(zip,"[Content_Types].xml",types);
   }
  }
  static XElement TextShape(string text,int index){
   return new XElement(P+"sp",new XElement(P+"nvSpPr",new XElement(P+"cNvPr",new XAttribute("id",2),new XAttribute("name","Text")),new XElement(P+"cNvSpPr",new XAttribute("txBox",1)),new XElement(P+"nvPr")),new XElement(P+"spPr",new XElement(A+"xfrm",new XElement(A+"off",new XAttribute("x",381000),new XAttribute("y",381000)),new XElement(A+"ext",new XAttribute("cx",11430000),new XAttribute("cy",6096000))),new XElement(A+"prstGeom",new XAttribute("prst","rect"),new XElement(A+"avLst"))),new XElement(P+"txBody",new XElement(A+"bodyPr",new XAttribute("wrap","square"),new XElement(A+"normAutofit")),new XElement(A+"lstStyle"),text.Split('\n').Select(line=>new XElement(A+"p",new XElement(A+"r",new XElement(A+"rPr",new XAttribute("lang","en-US"),new XAttribute("sz",2000)),new XElement(A+"t",line.TrimEnd('\r')))))));
  }
  public static void Slides(string output,DocumentContent content,bool pageImages){
   var chunks=new List<string>();if(!pageImages){var current=new StringBuilder();int lines=0;foreach(string line in content.Text.Replace("\r","").Split('\n')){for(int offset=0;offset<Math.Max(1,line.Length);offset+=120){string piece=line.Length==0?"":line.Substring(offset,Math.Min(120,line.Length-offset));if(current.Length+piece.Length>800||lines>=15){chunks.Add(current.ToString());current.Clear();lines=0;}current.AppendLine(piece);lines++;}}if(current.Length>0)chunks.Add(current.ToString());if(chunks.Count==0)throw new IOException("No text was found for creating slides.");}
   int count=pageImages?content.Pictures.Count:chunks.Count;if(count==0)throw new IOException("No PDF pages were rendered.");File.WriteAllBytes(output,RuntimePack.Read("Blank.pptx"));
   using(var zip=ZipFile.Open(output,ZipArchiveMode.Update)){
    var blank=ReadXml(zip,"ppt/slides/slide1.xml");var blankRels=ReadXml(zip,"ppt/slides/_rels/slide1.xml.rels");var pres=ReadXml(zip,"ppt/presentation.xml");var list=pres.Root.Element(P+"sldIdLst");if(list==null){list=new XElement(P+"sldIdLst");pres.Root.AddFirst(list);}list.RemoveNodes();
    var rels=ReadXml(zip,"ppt/_rels/presentation.xml.rels");rels.Root.Elements().Where(e=>((string)e.Attribute("Type")).EndsWith("/slide")).Remove();var types=ReadXml(zip,"[Content_Types].xml");types.Root.Elements(Ct+"Override").Where(e=>((string)e.Attribute("PartName")).StartsWith("/ppt/slides/slide")).Remove();ImageType(types);
    long sw=12192000,sh=6858000;var size=pres.Root.Element(P+"sldSz");size.SetAttributeValue("cx",sw);size.SetAttributeValue("cy",sh);
    for(int n=0;n<count;n++){
     string number=(n+1).ToString(),id="rIdRianSlide"+number;var slide=new XDocument(blank);var tree=slide.Root.Element(P+"cSld").Element(P+"spTree");tree.Elements().Where(e=>e.Name!=P+"nvGrpSpPr"&&e.Name!=P+"grpSpPr").Remove();var sr=new XDocument(blankRels);
     if(pageImages){string imageId="rIdRianPicture";string imagePart="ppt/media/rian-"+number+".png";PutFile(zip,imagePart,content.Pictures[n]);long cx,cy;using(var image=Image.FromFile(content.Pictures[n])){double scale=Math.Min((double)sw/image.Width,(double)sh/image.Height);cx=(long)(image.Width*scale);cy=(long)(image.Height*scale);}tree.Add(new XElement(P+"pic",new XElement(P+"nvPicPr",new XElement(P+"cNvPr",new XAttribute("id",2),new XAttribute("name","PDF page "+number)),new XElement(P+"cNvPicPr"),new XElement(P+"nvPr")),new XElement(P+"blipFill",new XElement(A+"blip",new XAttribute(R+"embed",imageId)),new XElement(A+"stretch",new XElement(A+"fillRect"))),new XElement(P+"spPr",new XElement(A+"xfrm",new XElement(A+"off",new XAttribute("x",(sw-cx)/2),new XAttribute("y",(sh-cy)/2)),new XElement(A+"ext",new XAttribute("cx",cx),new XAttribute("cy",cy))),new XElement(A+"prstGeom",new XAttribute("prst","rect"),new XElement(A+"avLst")))));sr.Root.Add(new XElement(Rel+"Relationship",new XAttribute("Id",imageId),new XAttribute("Type",R.NamespaceName+"/image"),new XAttribute("Target","../media/rian-"+number+".png")));}
     else tree.Add(TextShape(chunks[n],n));
     PutXml(zip,"ppt/slides/slide"+number+".xml",slide);PutXml(zip,"ppt/slides/_rels/slide"+number+".xml.rels",sr);list.Add(new XElement(P+"sldId",new XAttribute("id",256+n),new XAttribute(R+"id",id)));rels.Root.Add(new XElement(Rel+"Relationship",new XAttribute("Id",id),new XAttribute("Type",R.NamespaceName+"/slide"),new XAttribute("Target","slides/slide"+number+".xml")));types.Root.Add(new XElement(Ct+"Override",new XAttribute("PartName","/ppt/slides/slide"+number+".xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.presentationml.slide+xml")));
    }
    PutXml(zip,"ppt/presentation.xml",pres);PutXml(zip,"ppt/_rels/presentation.xml.rels",rels);PutXml(zip,"[Content_Types].xml",types);
   }
  }
 }
}
