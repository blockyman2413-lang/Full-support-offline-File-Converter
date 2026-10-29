using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using RianConverter;
static class UiTests {
 public static void Run(string root) {
  Application.EnableVisualStyles();
  Exception failure=null;int ticks=0;
  Control.CheckForIllegalCrossThreadCalls=true;
  using(var form=new MainForm())using(var timer=new Timer {Interval=50}) {
   timer.Tick+=delegate{ticks++;};
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   ((TextBox)typeof(MainForm).GetField("output",flags).GetValue(form)).Text=Path.Combine(root,"batch");
   ((CheckBox)typeof(MainForm).GetField("beside",flags).GetValue(form)).Checked=false;
   ((CheckBox)typeof(MainForm).GetField("open",flags).GetValue(form)).Checked=false;
   ((CheckBox)typeof(MainForm).GetField("rename",flags).GetValue(form)).Checked=true;
   ((TextBox)typeof(MainForm).GetField("lo",flags).GetValue(form)).Text="";
   form.Shown+=async delegate {
    try {
     typeof(MainForm).GetMethod("Add",flags).Invoke(form,new object[]{new[]{Path.Combine(root,"sample.docx"),Path.Combine(root,"corrupt.docx"),Path.Combine(root,"sample.pptx")}});
     timer.Start();await (Task)typeof(MainForm).GetMethod("RunQueue",flags).Invoke(form,null);timer.Stop();
     var rows=(ListView)typeof(MainForm).GetField("files",flags).GetValue(form);
     if(rows.Items[0].SubItems[3].Text!="Completed" || rows.Items[1].SubItems[3].Text!="Failed" || rows.Items[2].SubItems[3].Text!="Completed")throw new Exception("Queue did not continue after failure.");
     var progress=(ProgressBar)typeof(MainForm).GetField("progress",flags).GetValue(form);
     if(progress.Value!=3 || ticks<10)throw new Exception("Progress or UI responsiveness failed.");
     using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(root,"interface.png"));}
     string wav=Path.Combine(root,"ui-audio.wav");using(var writer=new BinaryWriter(File.Create(wav))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(8036);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(8000);writer.Write(new byte[8000]);}
     rows.Items.Clear();typeof(MainForm).GetMethod("Add",flags).Invoke(form,new object[]{new[]{Path.Combine(root,"fixture.png"),wav}});
     var formats=(ComboBox)typeof(MainForm).GetField("format",flags).GetValue(form);if(formats.Items.Cast<OutputFormat>().Any(f=>f.Extension==".docx"))throw new Exception("Output list includes an impossible conversion.");formats.SelectedItem=formats.Items.Cast<OutputFormat>().Single(f=>f.Extension==".mp3");
     if(rows.Items[0].SubItems[3].Text!="Unsupported")throw new Exception("Mixed-batch unsupported file not identified.");await (Task)typeof(MainForm).GetMethod("RunQueue",flags).Invoke(form,null);
     if(rows.Items[1].SubItems[3].Text!="Completed"||progress.Value!=1||progress.Maximum!=1)throw new Exception("Mixed-media queue failed.");
     Console.WriteLine("PASS mixed image/audio UI batch; incompatible file skipped; output list filtered; progress 1/1");
     using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(root,"media-interface.png"));}
     var tabs=(TabControl)typeof(MainForm).GetField("tabs",flags).GetValue(form);tabs.SelectedIndex=1;form.Refresh();
     using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,form.Width,form.Height));bmp.Save(Path.Combine(root,"settings.png"));}
     Console.WriteLine("PASS UI batch continues after failure; progress 3/3; UI timer ticks: "+ticks);
    }catch(Exception e){failure=e;}finally{form.Close();}
   };
   Application.Run(form);
  }
  if(failure!=null)throw failure;
 }
}
