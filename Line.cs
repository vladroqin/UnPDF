namespace UnPDF;

public class Line
{
  public float Y { get; private set; }
  public float LeftX { get; private set; }
  public float Size { get; private set; }
  Line() { }
  public Line(Glyph glyph)
  {
    Y = glyph.Y;
    LeftX = glyph.X;
    Size = glyph.Size;
  }
  /// <summary>
  /// Строка по-умолчанию
  /// </summary>
  /// <returns>Строка</returns>
  public static Line Default()
  {
    var newline = new Line
    {
      Y = 0.0f,
      LeftX = 0.0f,
      Size = 0.0f
    };
    return newline;
  }
}