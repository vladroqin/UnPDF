using System.Linq;
using System.Collections.Generic;
using System.Xml;

namespace UnPDF;

public class Font
{
  public string Id { get; set; }
  public string Name { get; set; }
  public string FullName { get; set; }
  public bool Bold { get; set; }
  public bool Italic { get; set; }
  public bool Sans { get; set; }
  public bool TW { get; set; }
  public bool Strike { get; set; }
  public bool UL { get; set; }
  public bool SC { get; set; }
  public bool? SupSub { get; set; }
  public Font(XmlNode node)
  {
    Id = node.GetString("id")!;
    Name = node.GetString("name")!;
    FullName = node.GetString("fullname")!;
    if (node.GetFloat("italicangle") != 0.0)
      Italic = true;
    FontNameAnalyze(Name);
  }
  /// <summary>
  /// Проанализировать имя шрифта
  /// </summary>
  /// <param name="name">Строка с именем</param>
  private void FontNameAnalyze(string name)
  {
    if (name.Contains("Bold") || name.Contains("Heavy")) Bold = true;
    if (name.Contains("Italic") || name.Contains("Oblique")) Italic = true;
    if (name.Contains("Helvetica") || name.Contains("Arial")) Sans = true;
    if (name.Contains("Courier")) TW = true;
  }
}