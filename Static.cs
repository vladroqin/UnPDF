using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml;

namespace UnPDF;

internal static class Static
{
  private const int DEF_WIN_CYR = 1251;
  private const int DEF_WIN_LAT = 1252;
  private const string SPACES = "\t \u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007"
      + "\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000\u180e\u200b\u200c\u200d\u2060\ufeff";
  private static readonly Encoding DEF_LAT_ENC = Encoding.GetEncoding(DEF_WIN_LAT, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
  private static readonly Encoding DEF_CYR_ENC = Encoding.GetEncoding(DEF_WIN_CYR);
  /// <summary>
  /// Восстановить строку из гекс-записи
  /// </summary>
  /// <param name="s">Строка с гекс-записью</param>
  /// <returns>Результат</returns>
  private static string RepairHexString(string s)
  {
    if (s.Length < 3 || s[0] != '<' || s[^1] != '>' || s.Length % 2 != 0)
      return s;
    byte[] newLine = new byte[(s.Length - 2) / 2];
    for (int i = 1; i < s.Length - 1; i++)
    {
      newLine[i / 2] = Convert.ToByte(s.Substring(i, 2), 16);
      i++;
    }
    return Encoding.GetEncoding(DEF_WIN_CYR).GetString(newLine);
  }
  /// <summary>
  /// Восстанавливает строку из восьмиричной записи
  /// </summary>
  /// <param name="s">Строка с восьмиричной записью</param>
  /// <returns>Результат</returns>
  private static string RepairOctalString(string s)
  {
    int inputLength = s.Length;
    var output = new List<byte>(inputLength);

    for (int i = 0; i < inputLength; i++)
    {
      if (s[i] == '\\')
      {
        if (inputLength >= i + 3 && s[i + 1].IsOctalDigit() && s[i + 2].IsOctalDigit()
          && s[i + 3].IsOctalDigit())
        {
          var x = Convert.ToByte(s.Substring(i + 1, 3), 8);
          output.Add(x);
          i += 3;
        }
        else throw new FormatException();
      }
      else
        output.Add((byte)s[i]);
    }
    return Encoding.GetEncoding(DEF_WIN_CYR).GetString(output.ToArray());
  }
  /// <summary>
  /// Вся ли строка состоит из АКСИ-симоволов
  /// </summary>
  /// <param name="s">Проверяемая строка</param>
  /// <returns>Да/нет</returns>
  internal static bool IsAllAscii(string s)
  {
    foreach (var c in s)
      if (c > 127) return false;
    return true;
  }
  /// <summary>
  /// Попадают ли символы в диапазон CP1252
  /// </summary>
  /// <param name="s">Строка</param>
  /// <returns>Да/нет</returns>
  internal static bool IsAllPseudoLatin1(string s)
  {
    foreach (var c in s)
    {
      if (c > 127 && (!(c >= 0xa0 && c <= 'ÿ')))
        return false;
      return true;
    }
    return true;
  }
  /// <summary>
  /// Может ли знак являться восьмеричной цифрой?
  /// </summary>
  /// <param name="c">Знак</param>
  /// <returns>Да/нет</returns>
  internal static bool IsOctalDigit(this char c)
  {
    if (c >= '0' && c <= '8') return true;
    else return false;
  }
  /// <summary>
  /// Получить нужную строку из узла КсМЛ
  /// </summary>
  /// <param name="node">Узел КсМЛ</param>
  /// <param name="s">Тип строки</param>
  /// <returns>Результат</returns>
  internal static bool TryRecode(string s, out string? result)
  {
    byte[]? preresult = null;
    result = null;

    try
    {
      preresult = DEF_LAT_ENC.GetBytes(s.ToCharArray());
    }
    catch (EncoderFallbackException)
    {
      return false;
    }

    result = DEF_CYR_ENC.GetString(preresult);
    return true;
  }
  /// <summary>
  /// Получи текст из XML-узла по наименованию аттрибута
  /// </summary>
  /// <param name="node">Узел</param>
  /// <param name="s">Наименование атрибута</param>
  /// <returns>Текст</returns>
  internal static string? GetString(this XmlNode node, string s) =>
       node.Attributes?.GetNamedItem(s)?.InnerText;
  /// <summary>
  /// Получить плавающее число из КсМЛ-узла
  /// </summary>
  /// <param name="node">Узел</param>
  /// <param name="s">Тип строки</param>
  /// <returns>Результат</returns>
  internal static float GetFloat(this XmlNode node, string s) =>
    Convert.ToSingle(node.GetString(s), CultureInfo.InvariantCulture);
  /// <summary>
  /// Получить строку из элемента КсСЛ
  /// </summary>
  /// <param name="root">Элемент</param>
  /// <param name="s">Тип строки</param>
  /// <returns>Результат</returns>
  internal static string? GetInnerString(this XmlElement root, string s) =>
    root?.GetElementsByTagName(s)?.Item(0)?.InnerText;
  /// <summary>
  /// Попытаться восстановить строку
  /// </summary>
  /// <param name="s">Строка для восстановления</param>
  /// <returns>Результат</returns>
  internal static string RepairString(string? s)
  {
    if (String.IsNullOrWhiteSpace(s)) return String.Empty;

    if (s[0] == '<' && s[^1] == '>' && s.Length % 2 == 0)
      return RepairHexString(s);
    else if (s.Contains('\\') && IsAllAscii(s))
    {
      try { return RepairOctalString(s); }
      catch (FormatException) { return s; }
    }
    //Так было бы лучше
    //else  if(TryRecode(s, out string? preresult))
    else if (IsAllPseudoLatin1(s))
    {
      if (TryRecode(s, out string? preresult)) //!
        return preresult!;
    }

    return s;
  }
  /// <summary>
  /// Предохранить от использования спецсимволов ХТМЛ
  /// </summary>
  /// <param name="s">Строка</param>
  /// <returns>Результат</returns>
  internal static string Preserve(this string s)
  {
    if (String.IsNullOrEmpty(s))
      return String.Empty;

    s = s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;");
    return s;
  }
  /// <summary>
  /// Получить последний символ из строки
  /// </summary>
  /// <param name="s">Строка</param>
  /// <returns>Последний символ</returns>
  internal static char LastChar(this string s)
  {
    int i = s.Length;
    char Char = i > 0 ? s[i - 1] : s[0];
    return Char;
  }
  /// <summary>
  /// Получить предпоследний символ из строки
  /// </summary>
  /// <param name="s">Строка</param>
  /// <returns>Предпоследний символ</returns>
  internal static char PenultChar(this string s)
  {
    int i = s.Length;
    char Char = i > 1 ? s[i - 2] : LastChar(s);
    return Char;
  }
  /// <summary>
  /// Удаляет лишние пробелы у дефисов
  /// </summary>
  /// <param name="s1">Первая строка</param>
  /// <param name="s2">Вторая строка</param>
  /// <param name="dash">Символ дефиса</param>
  internal static void DeleteDashes(ref string s1, ref string s2, char dash)
  {
    if (s1.Length > 1 && LastChar(s1) == dash && PenultChar(s1) != ' ')
      RenewStrings(ref s1, ref s2);
  }
  /// <summary>
  /// Обновить строки
  /// </summary>
  /// <param name="s1">Первая строка</param>
  /// <param name="s2">Вторая строка</param>
  internal static void RenewStrings(ref string s1, ref string s2)
  {
    int space = s2.IndexOf(' ');
    string rtHalfWord;
    if (space >= 0)
    {
      rtHalfWord = s2[..space];
      s2 = s2[(space + 1)..];
    }
    else
    {
      rtHalfWord = s2;
      s2 = String.Empty;
    }
    s1 = $"{s1}{rtHalfWord}";
  }
  /// <summary>
  /// Получение информации о шрифтах из текстового файла
  /// </summary>
  /// <param name="fontInfo">Информация о шрифтах</param>
  internal static void FontInfo(IEnumerable<Font> fontStorage, IEnumerable<string> fontInfo)
  {
    if (fontInfo == null) return;

    foreach (var l in fontInfo)
    {
      var cell = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      var font = fontStorage.FirstOrDefault(x => String.Equals(x.Name, cell[0]));
      if (font == null) continue;

      if (cell[1].Contains('b'))
        font.Bold = true;
      if (cell[1].Contains('i'))
        font.Italic = true;
    }
  }
  /// <summary>
  /// Формирование заголовка и информации об авторе
  /// </summary>
  /// <param name="root"></param>
  /// <param name="sb"></param>
  /// <returns>Файл с информацией</returns>
  internal static StringBuilder FirstTime(XmlElement root)
  {
    StringBuilder sb = new();
    if (root == null)
      return sb;

    string author = RepairString(root.GetInnerString("Author")).Preserve();
    string title = RepairString(root.GetInnerString("Title")).Preserve();
    sb = sb.AppendLine($"<title>{title}</title>");
    if (!String.IsNullOrWhiteSpace(author))
      sb.AppendLine($"<meta name=\"author\" content=\"{author}\">");
    sb.AppendLine("<style></style>");
    return sb;
  }
  /// <summary>
  /// Убрать лишние пробелы
  /// </summary>
  /// <param name="sb">Где надо убрать</param>
  /// <returns>Реззультирующий массив строк</returns>
  internal static string[] AfterExtraction(StringBuilder sb)
  {
    string forWork = sb.ToString();

    var result = Regex.Replace(forWork, $"[{SPACES}]+", " ").
      Replace("\n ", "\n").
      Replace(" \n", "\n").
      Replace("<p> ", "<p>");

    return result.Split('\n');
  }

  /// <summary>
  /// Задать время модификации по максимальной дате создания или правки
  /// </summary>
  /// <param name="xml">XML</param>
  /// <param name="file">Файл</param>
  internal static DateTime BeforeEnd(XmlElement xml)
  {
    string? creation = xml.GetInnerString("CreationDate");
    string? mod = xml.GetInnerString("ModDate");
    DateTime.TryParse(creation, out DateTime ctime);
    DateTime.TryParse(mod, out DateTime mtime);
    return mtime >= ctime ? mtime : ctime;
  }
  /// <summary>
  /// Выполнить комманду
  /// </summary>
  /// <param name="cmd">Имя команды</param>
  /// <param name="args">Аргументы</param>
  /// <param name="workingDir">Рабочая директория</param>
  /// <param name="f">Функция для работы с выводом и возврата числа</param>
  /// <returns>Код возврата</returns>
  internal static int Command(string cmd, string args, string? workingDir = null, Func<StreamReader, int>? f = null)
  {
    using Process process = new();
    ProcessStartInfo startInfo = new()
    {
      // У меня тут в одном месте рабочая директория ещё не указывается
      // и это может в конце концов выйти боком. Но пока не выходит ☺
      WorkingDirectory = workingDir,
      WindowStyle = ProcessWindowStyle.Hidden,
      FileName = cmd,
      Arguments = args,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
    };
    process.StartInfo = startInfo;
    process.Start();
    process.WaitForExit();

    if (f != null)
    {
      return f(process.StandardOutput);
    }
    else
    {
      return process.ExitCode;
    }
  }
  /// <summary>
  /// Печатать ошибки красным
  /// </summary>
  /// <param name="s">Текст ошибки</param>
  internal static void PrintRed(string s)
  {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(s);
    Console.ResetColor();
  }
}