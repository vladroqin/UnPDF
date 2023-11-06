using System.Linq;
using System.Collections.Generic;
using System.Xml;

namespace UnPDF0;

public class Font
{
  public string Id;
  public string Name;
  public string FullName;
  public bool Bold;
  public bool Italic;
  public bool Sans;
  public bool TW;
  public bool Strike;
  public bool UL;
  public bool SC;
  public bool? SupSub;
  public Font(XmlNode node)
  {
    Id = node.GetString("id");
    Name = node.GetString("name");
    FullName = node.GetString("fullname");
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
  public static readonly HashSet<Font> Storage = new();
  /// <summary>
  /// Добавить шрифт в хранилище
  /// </summary>
  /// <param name="font">Шрифт</param>
  public static void Add(Font font) => Storage.Add(font);
  /// <summary>
  /// Получить шрифт
  /// </summary>
  /// <param name="s">Название шрифта</param>
  /// <returns></returns>
  public static Font GetFont(string s) => Storage.First(x => x.Id == s);
}