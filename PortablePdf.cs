using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace RianConverter {
 public sealed class PortablePdf : IDisposable {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibrary(string path);
  [DllImport("pdfium.dll")] static extern void FPDF_InitLibrary();
  [DllImport("pdfium.dll")] static extern IntPtr FPDF_LoadMemDocument(IntPtr data,int length,IntPtr password);
  [DllImport("pdfium.dll")] static extern int FPDF_GetPageCount(IntPtr doc);
  [DllImport("pdfium.dll")] static extern IntPtr FPDF_LoadPage(IntPtr doc,int index);
  [DllImport("pdfium.dll")] static extern double FPDF_GetPageWidth(IntPtr page);
  [DllImport("pdfium.dll")] static extern double FPDF_GetPageHeight(IntPtr page);
  [DllImport("pdfium.dll")] static extern IntPtr FPDFBitmap_CreateEx(int width,int height,int format,IntPtr buffer,int stride);
  [DllImport("pdfium.dll")] static extern void FPDFBitmap_Destroy(IntPtr bitmap);
  [DllImport("pdfium.dll")] static extern void FPDF_RenderPageBitmap(IntPtr bitmap,IntPtr page,int x,int y,int width,int height,int rotate,int flags);
  [DllImport("pdfium.dll")] static extern IntPtr FPDFText_LoadPage(IntPtr page);
  [DllImport("pdfium.dll")] static extern int FPDFText_CountChars(IntPtr text);
  [DllImport("pdfium.dll")] static extern int FPDFText_GetText(IntPtr text,int start,int count,[Out] byte[] buffer);
  [DllImport("pdfium.dll")] static extern void FPDFText_ClosePage(IntPtr text);
  [DllImport("pdfium.dll")] static extern void FPDF_ClosePage(IntPtr page);
  [DllImport("pdfium.dll")] static extern void FPDF_CloseDocument(IntPtr doc);
  static PortablePdf(){if(LoadLibrary(RuntimePack.Tool("pdfium.dll"))==IntPtr.Zero)throw new IOException("The bundled PDF engine could not load. Windows 10 or newer is required.");FPDF_InitLibrary();}
  IntPtr document;GCHandle pinned;
  public int Count {get;private set;}
  public PortablePdf(string path){
   if(new FileInfo(path).Length>512L*1024*1024)throw new IOException("PDF files above 512 MB are not supported by this lightweight converter.");
   byte[] bytes=File.ReadAllBytes(path);pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);document=FPDF_LoadMemDocument(pinned.AddrOfPinnedObject(),bytes.Length,IntPtr.Zero);
   if(document==IntPtr.Zero){pinned.Free();throw new IOException("This PDF is damaged or password protected.");}Count=FPDF_GetPageCount(document);if(Count<1){Dispose();throw new IOException("This PDF contains no pages.");}
  }
  public string Text(int index){IntPtr page=FPDF_LoadPage(document,index);if(page==IntPtr.Zero)throw new IOException("Cannot read PDF page "+(index+1));try{IntPtr text=FPDFText_LoadPage(page);if(text==IntPtr.Zero)return "";try{int count=FPDFText_CountChars(text);if(count<0||count>10000000)throw new IOException("PDF text is too large to process.");byte[] bytes=new byte[(count+1)*2];int read=FPDFText_GetText(text,0,count,bytes);return read>0?Encoding.Unicode.GetString(bytes,0,(read-1)*2):"";}finally{FPDFText_ClosePage(text);}}finally{FPDF_ClosePage(page);}}
  public Bitmap Render(int index){
   IntPtr page=FPDF_LoadPage(document,index);if(page==IntPtr.Zero)throw new IOException("Cannot render PDF page "+(index+1));
   try {double pw=FPDF_GetPageWidth(page),ph=FPDF_GetPageHeight(page);if(pw<=0||ph<=0||Double.IsNaN(pw)||Double.IsNaN(ph))throw new IOException("Invalid PDF page size.");double scale=Math.Min(2,Math.Min(6000/pw,6000/ph));int w=Math.Max(1,(int)(pw*scale)),h=Math.Max(1,(int)(ph*scale));if((long)w*h>24000000)throw new IOException("PDF page is too large to render safely.");
    var image=new Bitmap(w,h,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(image))g.Clear(Color.White);var data=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
    bool rendered=false;try {IntPtr bitmap=FPDFBitmap_CreateEx(w,h,4,data.Scan0,data.Stride);if(bitmap==IntPtr.Zero)throw new IOException("PDF bitmap allocation failed.");try{FPDF_RenderPageBitmap(bitmap,page,0,0,w,h,0,1);}finally{FPDFBitmap_Destroy(bitmap);}rendered=true;}finally{image.UnlockBits(data);if(!rendered)image.Dispose();}return image;
   }finally{FPDF_ClosePage(page);}
  }
  public void Dispose(){if(document!=IntPtr.Zero){FPDF_CloseDocument(document);document=IntPtr.Zero;}if(pinned.IsAllocated)pinned.Free();}
  public static void Write(string output,string text,IEnumerable<string> pictures){
   using(var pdf=new PdfDocument()){
    PdfPage page=null;XGraphics graphics=null;double y=0;var font=new XFont("Arial",11,XFontStyle.Regular,new XPdfFontOptions(PdfFontEncoding.Unicode));
    Action next=delegate{if(graphics!=null)graphics.Dispose();page=pdf.AddPage();graphics=XGraphics.FromPdfPage(page);y=45;};next();
    try {
     foreach(string paragraph in text.Replace("\r","").Split('\n')){
      string line="";foreach(string word in paragraph.Split(' ')){
       if(graphics.MeasureString(line+word,font).Width>page.Width.Point-90 && line.Length>0){if(y>page.Height.Point-50)next();graphics.DrawString(line,font,XBrushes.Black,new XPoint(45,y));y+=16;line="";}
       // Split very long unbroken tokens so text is never silently clipped.
       foreach(char c in word){if(graphics.MeasureString(line+c,font).Width>page.Width.Point-90){if(y>page.Height.Point-50)next();graphics.DrawString(line,font,XBrushes.Black,new XPoint(45,y));y+=16;line="";}line+=c;}line+=" ";
      }
      if(y>page.Height.Point-50)next();graphics.DrawString(line,font,XBrushes.Black,new XPoint(45,y));y+=18;
     }
     foreach(string path in pictures){using(var image=XImage.FromFile(path)){if(y>60)next();double ratio=Math.Min((page.Width.Point-90)/image.PixelWidth,(page.Height.Point-90)/image.PixelHeight);graphics.DrawImage(image,45,45,image.PixelWidth*ratio,image.PixelHeight*ratio);y=page.Height.Point;}}
     graphics.Dispose();graphics=null;pdf.Save(output);
    }finally{if(graphics!=null)graphics.Dispose();}
   }
  }
 }
}
