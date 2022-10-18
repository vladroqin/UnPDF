using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using WeCantSpell.Hunspell;
using static FuckPDF0.General;

namespace FuckPDF0;

class Program
{
  private static readonly string SPACES =
      "[\t \u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007"
      + "\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000\u180e\u200b\u200c\u200d\u2060\ufeff]";
  public static Glyph prevGlyph;

  public static Line line;

  public static bool hyphens = true;
  public static void Main(string[] args)
  {
    if (args.Length < 1)
      return;

    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var sb = new StringBuilder();
    string? fontInfo = null;
    string newFile = $"{Path.GetFileNameWithoutExtension(args[0])}.htm";
    string AFF;
    string DIC;
    try
    {
      AFF = ConfigurationManager.AppSettings["AFF"];
      DIC = ConfigurationManager.AppSettings["DIC"];
    }
    catch (Exception) { return; }

    if (args.Length > 1)
    {
      for (int i = 1; i < args.Length; i++)
      {
        if (args[i] == "--font-info" && args.Length > i + 1)
          fontInfo = args[i + 1];
        if (args[i] == "--no-hyphens")
          hyphens = false;
      }
    }

    var doc = new XmlDocument();
    doc.Load(args[0]);
    var root = doc.DocumentElement;
    sb = FirstTime(root, sb);

    var fonts = root.GetElementsByTagName("Font");
    foreach (XmlNode font in fonts)
      Font.Add(new Font(font));
    FontInfo(fontInfo);

    var allPages = root.GetElementsByTagName("Page");
    foreach (XmlNode page in allPages)
    {
      prevGlyph = Glyph.Default();
      line = Line.Default();
      var content = page.LastChild;
      foreach (XmlNode glyph in content)
      {
        if (glyph.Name != "Glyph")
          continue;
        var a = new Glyph(glyph);
        #region end tags
        if (prevGlyph.Font.SupSub == false & a.Y > prevGlyph.Y & a.Size > prevGlyph.Size)
        {
          sb.Append("</sub>");
          a.Font.SupSub = null;
        }
        if (prevGlyph.Font.SupSub == true & a.Y < prevGlyph.Y & a.Size > prevGlyph.Size)
        {
          sb.Append("</sup>");
          a.Font.SupSub = null;
        }
        if (!a.Font.Bold && prevGlyph.Font.Bold)
          sb.Append("</b>");
        if (!a.Font.Italic && prevGlyph.Font.Italic)
          sb.Append("</i>");
        #endregion

        if (a.Y <= (prevGlyph.Y - prevGlyph.Size) || (a.Y - prevGlyph.Y > a.Size))
        {
          sb.Append("\n");
          if ((a.X - line.LeftX) > 1) //!
            sb.Append("<p>");
          line = new Line(a);
        }

        #region start tags
        if (a.Font.Italic && !prevGlyph.Font.Italic)
          sb.Append("<i>");
        if (a.Font.Bold && !prevGlyph.Font.Bold)
          sb.Append("<b>");
        if (a.Y > prevGlyph.Y && a.Size < prevGlyph.Size)
        {
          a.Font.SupSub = true;
          sb.Append("<sup>");
        }
        if (a.Y < prevGlyph.Y && a.Size < prevGlyph.Size)
        {
          a.Font.SupSub = false;
          sb.Append("<sub>");
        }
        #endregion

        if (glyph.InnerText == "")
          sb.Append(" ");
        else
        {
          if ((a.X - Math.Abs(prevGlyph.X + prevGlyph.Width)) < 1.0 && prevGlyph.X != 0.0) //0.4!!!
            sb.Append(Preserve(glyph.InnerText));
          else
            sb.Append($" {Preserve(glyph.InnerText)}");
        }
        prevGlyph = a;
      }
      #region End of Page
      if (prevGlyph.Font.SupSub == true)
        sb.Append("</sup>");
      if (prevGlyph.Font.SupSub == false)
        sb.Append("</sub>");
      if (prevGlyph.Font.Italic)
        sb.Append("</i>");
      if (prevGlyph.Font.Bold)
        sb.Append("</b>");
      sb.Append("<hr>");
      #endregion
    }
    sb = AfterExtraction(sb);
    var strings = sb.ToString().Split('\n');

    using (var dic = File.OpenRead(DIC))
    using (var aff = File.OpenRead(AFF))
    {
      var spell = WordList.CreateFromStreams(dic, aff);
      for (int i = 0; i < strings.Length - 1; i++)
      {
        DeleteDashes(ref strings[i], ref strings[i + 1], '–');
        DeleteDashes(ref strings[i], ref strings[i + 1], '—');
        DeleteHyphens(ref strings[i], ref strings[i + 1], spell);
      }
    }
    File.WriteAllLines(newFile, strings);
    BeforeEnd(root, newFile);
  }
  /// <summary>
  /// Получение информации о шрифтах из текстового файла
  /// </summary>
  /// <param name="fontInfo">Информация о шрифтах</param>
  private static void FontInfo(string fontInfo)
  {
    if (fontInfo != null)
    {
      var fi = File.ReadAllLines(fontInfo);
      foreach (var l in fi)
      {
        var cell = l.Split(' ');
        var font = Font.Storage.First(x => String.Equals(x.Name, cell[0]));
        if (cell[1].Contains("b"))
          font.Bold = true;
        if (cell[1].Contains("i"))
          font.Italic = true;
      }
    }
  }
  /// <summary>
  /// Формирование заголовка и информации об авторе
  /// </summary>
  /// <param name="root"></param>
  /// <param name="sb"></param>
  /// <returns>Файл с информацией</returns>
  private static StringBuilder FirstTime(XmlElement root, StringBuilder sb)
  {
    string? author = RepairString(root.GetInnerString("Author"));
    string? title = RepairString(root.GetInnerString("Title"));
    sb = sb.Append($"<meta charset=\"utf-8\"><title>{title}</title>\n");
    if (!String.IsNullOrWhiteSpace(author))
      sb = sb.Append($"<meta name=\"author\" content=\"{author}\">\n<style></style>");
    return sb;
  }
  /// <summary>
  /// Убрать лишние пробелы
  /// </summary>
  /// <param name="sb">Текст до обработки</param>
  /// <returns>Текст без лишних пробелов</returns>
  private static StringBuilder AfterExtraction(StringBuilder sb)
  {
    //Увы
    string forWork = sb.ToString();
    var result = new StringBuilder(Regex.Replace(forWork, $"[{SPACES}]+", " "));
    result = result.Replace("\n ", "\n").Replace(" \n", "\n").Replace("<p> ", "<p>");
    return result;
  }
  /// <summary>
  /// Задать время модификации по максимальной дате создания или правки
  /// </summary>
  /// <param name="xml">КсМЛ</param>
  /// <param name="file">Файл</param>
  private static void BeforeEnd(XmlElement xml, string file)
  {
    string creation = xml.GetInnerString("CreationDate");
    string mod = xml.GetInnerString("ModDate");
    DateTime.TryParse(creation, out DateTime ctime);
    DateTime.TryParse(mod, out DateTime mtime);
    File.SetLastWriteTime(file, (mtime >= ctime ? mtime : ctime));
  }
}