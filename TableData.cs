using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace RianConverter {
 public static class TableData {
  public static void Convert(string input,string output){
   var columns=new List<string>();var rows=new List<string[]>();string source=Formats.Ext(input),target=Formats.Ext(output);string text=File.ReadAllText(input,new UTF8Encoding(false,true));
   if(source==".csv"||source==".tsv"){
    var records=Parse(text,source==".csv"?',':'\t');if(records.Count==0)throw new IOException("The table is empty.");columns.AddRange(records[0]);foreach(var row in records.Skip(1)){if(row.Length!=columns.Count)throw new IOException("Rows have inconsistent column counts.");rows.Add(row);}
   }else if(source==".json"){
    object data=new JavaScriptSerializer {MaxJsonLength=32*1024*1024,RecursionLimit=32}.DeserializeObject(text);var array=data as object[];if(array==null)throw new IOException("JSON input must be an array of flat row objects.");
    foreach(var item in array){var dict=item as Dictionary<string,object>;if(dict==null)throw new IOException("JSON rows must be objects.");foreach(var pair in dict){if(pair.Value is IDictionary || pair.Value is object[])throw new IOException("Nested JSON is not supported by table conversion.");if(!columns.Contains(pair.Key))columns.Add(pair.Key);}}
    foreach(Dictionary<string,object> item in array)rows.Add(columns.Select(c=>item.ContainsKey(c)?System.Convert.ToString(item[c],System.Globalization.CultureInfo.InvariantCulture):"").ToArray());
   }else{
    XDocument xml;using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))xml=XDocument.Load(reader);
    if(xml.Root==null||xml.Root.Name!="table"||xml.Root.Element("columns")==null||xml.Root.Element("rows")==null)throw new IOException("XML input must use RianConverter's table/columns/rows structure.");
    columns.AddRange(xml.Root.Element("columns").Elements("column").Select(c=>(string)c.Attribute("name")??""));
    foreach(var row in xml.Root.Element("rows").Elements("row")){var values=row.Elements("value").Select(v=>v.Value).ToArray();if(values.Length!=columns.Count)throw new IOException("XML row length does not match its columns.");rows.Add(values);}
   }
   if(columns.Count==0||columns.Any(String.IsNullOrWhiteSpace)||columns.Distinct(StringComparer.Ordinal).Count()!=columns.Count)throw new IOException("Table headers must be non-empty and unique.");
   if(target==".json"){
    var items=rows.Select(row=>columns.Select((c,n)=>new {c,n}).ToDictionary(x=>x.c,x=>row[x.n])).ToArray();File.WriteAllText(output,new JavaScriptSerializer {MaxJsonLength=32*1024*1024}.Serialize(items),new UTF8Encoding(false));
   }else if(target==".xml"){
    new XDocument(new XElement("table",new XElement("columns",columns.Select(c=>new XElement("column",new XAttribute("name",c)))),new XElement("rows",rows.Select(row=>new XElement("row",row.Select(value=>new XElement("value",value))))))).Save(output);
   }else{char delimiter=target==".csv"?',':'\t';Func<string,string> escape=s=>s.IndexOfAny(new[]{delimiter,'"','\r','\n'})>=0?"\""+s.Replace("\"","\"\"")+"\"":s;var lines=new[]{columns.ToArray()}.Concat(rows).Select(row=>String.Join(delimiter.ToString(),row.Select(escape)));File.WriteAllText(output,String.Join("\r\n",lines)+"\r\n",new UTF8Encoding(true));}
  }
  static List<string[]> Parse(string text,char delimiter){
   var result=new List<string[]>();var row=new List<string>();var value=new StringBuilder();bool quoted=false,closed=false,started=false;
   for(int n=0;n<text.Length;n++){char c=text[n];started=true;if(quoted){if(c=='"'){if(n+1<text.Length&&text[n+1]=='"'){value.Append('"');n++;}else{quoted=false;closed=true;}}else value.Append(c);continue;}
    if(c=='"'){if(value.Length!=0||closed)throw new IOException("Invalid quoted table field.");quoted=true;}
    else if(c==delimiter){row.Add(value.ToString());value.Clear();closed=false;}
    else if(c=='\r'||c=='\n'){if(c=='\r'&&n+1<text.Length&&text[n+1]=='\n')n++;row.Add(value.ToString());result.Add(row.ToArray());row.Clear();value.Clear();closed=false;started=false;}
    else{if(closed)throw new IOException("Unexpected text after a quoted table field.");value.Append(c);}
   }
   if(quoted)throw new IOException("Unclosed quoted table field.");if(started||row.Count>0||value.Length>0){row.Add(value.ToString());result.Add(row.ToArray());}return result;
  }
 }
}
