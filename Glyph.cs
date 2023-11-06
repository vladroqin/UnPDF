using System.Xml;
using System.Linq;

namespace UnPDF0;

public class Glyph
{
  public string FontId;
  public float Size;
  public float X;
  public float Y;
  public float Width;
  public Glyph(XmlNode node)
  {
    FontId = node.GetString("font");
    Size = node.GetFloat("size");
    X = node.GetFloat("x");
    Y = node.GetFloat("y");
    Width = node.GetFloat("width");
  }
  public Glyph() { }
  /// <summary>
  /// Символ по-умолчанию
  /// </summary>
  /// <returns>Символ</returns>
  public static Glyph Default()
  {
    var x = new Glyph();
    x.FontId = Font.Storage.First().Id;
    return x;
  }
  public Font Font { get { return Font.GetFont(FontId); } }
}