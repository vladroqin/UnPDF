using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using WeCantSpell.Hunspell;

namespace UnPDF;

public class WorkWPdfs
{
  private const long PDF_MAX_SIZE = 1_000_000L;  // или 1024*1024?
  private readonly UnPdfSettings _sets;
  private readonly Regex _endWord = new(@"([\w\.]+)-$", RegexOptions.Compiled);
  private readonly Regex _firstWord = new(@"^(\w+)", RegexOptions.Compiled);
  private readonly string _params;
  private string? _tempDir;
  private int _pages;
  private FileInfo _fi;
  private Glyph? prevGlyph;
  private Line? line;
  /// <summary>
  /// Работаем с файлом PDF
  /// </summary>
  public WorkWPdfs(CLParse args)
  {
    _sets = args.Parse();
    _fi = new(_sets.InputFile);
    _params = CreateParamsString();
  }
  /// <summary>
  /// Возвращаем количество страниц
  /// </summary>
  /// <param name="stream">Поток</param>
  /// <returns>Количество страниц</returns>
  private static int FindStringWPages(StreamReader stream)
  {
    int result = 0;
    while (!stream.EndOfStream)
    {
      var line = stream.ReadLine();
      if (line != null && line.StartsWith("Pages: "))
      {
        result = Int32.Parse(line[7..]);
        break;
      }
    }
    return result;
  }
  /// <summary>
  /// Работаем с файлом
  /// </summary>
  public void WorkWFile()
  {
    if (_sets.InputFile.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
    {
      WorkWPdf(_sets.InputFile);
    }
    else
    {
      WorkWOneTetml(_sets.InputFile);
    }

    if (_tempDir != null)
    {
      #if !DEBUG
      Directory.Delete(_tempDir, true);
      #endif
    }
  }
  /// <summary>
  /// Работаем с файлом TETML
  /// </summary>
  private void WorkWOneTetml(string tetmlFile)
  {
    var doc = new XmlDocument();
    doc.Load(tetmlFile);
    var root = doc.DocumentElement;
    var sb = Static.FirstTime(root!);
    sb = TetmlTextExtract(root!, sb);
    var preresult = Static.AfterExtraction(sb);
    var result = CleaningUp(preresult);

    File.WriteAllLines(_sets.OutputFile, result);

    var resultTime = LastTime(root!);
    File.SetLastWriteTime(_sets.OutputFile, resultTime);
  }
  private void WorkWPdf(string pdfFile)
  {
    _pages = Static.Command("mutool", $"info \"{pdfFile}\"", null, FindStringWPages);
    _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(_tempDir);

    if (_sets.FontMap != null)
    {
      // А это корректно вообще? 🙃
      var clglfiles = Directory.GetFiles(".", "*.?l");
      foreach (var f in clglfiles)
      {
        File.Copy(f, Path.Combine(_tempDir, f));
      }
    }

    var op = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    Parallel.For(1, _pages + 1, op, SplitPdf);

    var doc = new XmlDocument();
    doc.Load(Path.Combine(_tempDir, "0001.tetml"));
    var root = doc.DocumentElement;
    var sb = Static.FirstTime(root!);

    for (int i = 1; i <= _pages; i++)
    {
      var tmp = new XmlDocument();
      tmp.Load(Path.Combine(_tempDir, $"{i:0000}.tetml"));
      var tmpRoot = tmp.DocumentElement;
      sb = TetmlTextExtract(tmpRoot!, sb);
    }

    var preresult = Static.AfterExtraction(sb);
    var result = CleaningUp(preresult);

    #if DEBUG
    Console.WriteLine("That's all, folks!");
    #endif

    File.WriteAllLines(_sets.OutputFile, result);

    var resultTime = LastTime(root!);
    File.SetLastWriteTime(_sets.OutputFile, resultTime);
  }
  /// <summary>
  /// Разрезать PDF постранично и конвертировать в TETML
  /// </summary>
  /// <param name="page">Количество страниц</param>
  private void SplitPdf(int page)
  {
    string pdfFile = $"{page:0000}.pdf";
    string fullName = Path.Combine(_tempDir!, pdfFile);

    Command("mutool", $"clean -Dggggzfic \"{_fi.FullName}\" {pdfFile} {page}");

    // ГРЯЗНЫЙ ХАК!!!
    long size = new FileInfo(fullName).Length;
    if (size >= PDF_MAX_SIZE)
    {
      Command("ps2pdf14", pdfFile);
      File.Delete(fullName);
      File.Move(Path.Combine(_tempDir!, $"{pdfFile}.pdf"), fullName);
    }

    string tetParams = String.Format(_params, page);
    Command("tet", tetParams);
    #if DEBUG
    Console.WriteLine($"Page: {page}");
    #endif
  }

  /// <summary>
  /// Выполнять команду скрытно (с возможностью обработки вывода)
  /// </summary>
  /// <param name="cmd">Команда</param>
  /// <param name="args">Параметры</param>
  /// <param name="f">Функция для обработки вывода</param>
  /// <returns>Возвращаемое целое</returns>
  private void Command(string cmd, string args) =>
    Static.Command(cmd, args, _tempDir);
  /// <summary>
  /// Пытаюсь получить дату из файла, а если не получаю - ставлю
  /// такую же как и у оригинального PDF.
  /// </summary>
  /// <param name="xml">TETML сюда</param>
  /// <returns>Время модификации</returns>
  private DateTime LastTime(XmlElement xml)
  {
    var result = Static.BeforeEnd(xml);
    if (result == default)
      result = _fi.LastWriteTime;
    return result;
  }
  /// <summary>
  /// Создать строку параметров к tet
  /// (с FontMap)
  /// </summary>
  /// <returns>Строка параметров</returns>
  private string CreateParamsString()
  {
    if (_sets.FontMap == null)
      return "-m glyph {0:0000}.pdf";

    List<(string, string)> clList = [];
    List<(string, string)> glList = [];

    foreach (var line in _sets.FontMap!)
    {
      Span<string> oneString = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      string fnwoext = oneString[1][..^3];
      var forAdd = (oneString[0], fnwoext);

      if (oneString[1].EndsWith(".cl", StringComparison.InvariantCultureIgnoreCase))
      {
        clList.Add(forAdd);
      }
      else
      {
        glList.Add(forAdd);
      }
    }

    StringBuilder result = new("--docopt \"normalize=nfc glyphmapping {");
    if (clList.Count > 0)
    {
      foreach (var x in clList)
      {
        var forAppend = String.Format("{{fontname={0} codelist={1}}}", x.Item1, x.Item2);
        result.Append(forAppend);
      }
    }
    if (glList.Count > 0)
    {
      foreach (var x in glList)
      {
        var forAppend = String.Format("{{fontname={0} glyphlist={1}}}", x.Item1, x.Item2);
        result.Append(forAppend);
      }
    }
    result.Append("}\" -m glyph {0:0000}.pdf");

    return result.ToString();
  }
  /// <summary>
  /// Превращаем XML в HTML
  /// </summary>
  /// <param name="root">XML</param>
  /// <param name="sb">Где должны работать</param>
  /// <returns>С результатом</returns>
  private StringBuilder TetmlTextExtract(XmlElement root, StringBuilder sb)
  {
    var fonts = root.GetElementsByTagName("Font");
    HashSet<Font> fontHashSet = [];

    foreach (XmlNode font in fonts)
      fontHashSet.Add(new Font(font));
    if (fontHashSet.Count == 0)
    {
      sb.AppendLine("<hr>");
      return sb;
    }
    Static.FontInfo(fontHashSet, _sets.FontInfo!);

    var allPages = root.GetElementsByTagName("Page");
    foreach (XmlNode page in allPages)
    {
      prevGlyph = new Glyph(fontHashSet);
      line = Line.Default();
      var content = page.LastChild;
      foreach (XmlNode glyph in content!)
      {
        if (glyph.Name != "Glyph")
          continue;
        var a = new Glyph(glyph, fontHashSet);
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
          sb.AppendLine();
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
          sb.Append(' ');
        else
        {
          if ((a.X - Math.Abs(prevGlyph.X + prevGlyph.Width)) < 1.0 && prevGlyph.X != 0.0) //0.4!!!
            sb.Append(glyph.InnerText.Preserve());
          else
            sb.Append($" {glyph.InnerText.Preserve()}");
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
    return sb;
  }
  /// <summary>
  /// Удаляет переносы из строк
  /// </summary>
  /// <param name="s1">Первая строка</param>
  /// <param name="s2">Вторая строка</param>
  /// <param name="spell">Проверка правописания</param>
  private void DeleteHyphens(ref string s1, ref string s2, WordList spell)
  {
    if (_sets.EnableHyphens &&
        s1.Length > 1 &&
        Static.LastChar(s1) == '-' &&
        Static.PenultChar(s1) != ' ' &&
        Static.PenultChar(s1) != '»')
    {
      var x = _endWord.Match(s1);
      var y = _firstWord.Match(s2);

      if (x.Length < 1 || y.Length < 1) return;

      char xp = x.ToString().PenultChar();
      if (Char.IsNumber(y.ToString()[0]) ||
          Char.IsNumber(xp) ||
          Char.IsUpper(s2[0]) ||
          xp == '.')
      {
        Static.RenewStrings(ref s1, ref s2);
        return;
      }

      var z = $"{x.ToString()[..^1]}{y}";
      if (spell.Check(z))
      {
        s1 = s1[..^1];
        Static.RenewStrings(ref s1, ref s2);
        return;
      }

      /*if (spell.Check($"{x}{y}"))
        RenewStrings(ref s1, ref s2);*/
    }
  }
  /// <summary>
  /// Подчищаем
  /// </summary>
  /// <param name="strings">Массив строк</param>
  /// <returns>Результат</returns>
  private string[] CleaningUp(string[] strings)
  {
    using var dic = File.OpenRead(_sets.Dic);
    using var aff = File.OpenRead(_sets.Aff);

    var spell = WordList.CreateFromStreams(dic, aff);
    for (int i = 0; i < strings.Length - 1; i++)
    {
      Static.DeleteDashes(ref strings[i], ref strings[i + 1], '–');
      Static.DeleteDashes(ref strings[i], ref strings[i + 1], '—');
      DeleteHyphens(ref strings[i], ref strings[i + 1], spell);
    }

    return strings;
  }
}