using System.Xml;

namespace UnPDF;

public class Glyph
{
  private HashSet<Font> _hs;
  public string FontId { get; set; }
  public float Size { get; set; }
  public float X { get; set; }
  public float Y { get; set; }
  public float Width { get; set; }
  public Glyph(XmlNode node, HashSet<Font> hs)
  {
    _hs = hs;
    FontId = node.GetString("font")!;
    Size = node.GetFloat("size");
    X = node.GetFloat("x");
    Y = node.GetFloat("y");
    Width = node.GetFloat("width");
  }
  public Glyph(HashSet<Font> hs)
  {
    _hs = hs;
    FontId = _hs.First().Id;
  }
  public Font Font => _hs.First(x => x.Id == FontId);
}