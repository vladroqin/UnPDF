using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using WeCantSpell.Hunspell;

namespace FuckPDF0;

public static class General
{
  private static Regex _endWord = new Regex(@"([\w\.]+)-$", RegexOptions.Compiled);
  private static Regex _firstWord = new Regex(@"^(\w+)", RegexOptions.Compiled);
  private const int DEF_WIN_CYR = 1251;
  private const int DEF_WIN_LAT = 1252;
  private static Lazy<Encoding> DEF_LAT_ENC = new Lazy<Encoding>(() =>
    Encoding.GetEncoding(DEF_WIN_LAT, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback));
  private static Lazy<Encoding> DEF_CYR_ENC = new Lazy<Encoding>(() => Encoding.GetEncoding(DEF_WIN_CYR));

  /// <summary>
  /// Вся ли строка состоит из АКСИ-симоволов
  /// </summary>
  /// <param name="s">Проверяемая строка</param>
  /// <returns>Да/нет</returns>
  private static bool _isAllAscii(string s)
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
  private static bool _isAllPseudoLatin1(string s)
  {
    foreach (var c in s)
    {
      if ( c > 127 && (!(c >= 'À' && c <= 'ÿ') && c != 0xa8 && c != 0xb8))
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
  private static bool _isOctalDigit(this char c)
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
  public static bool TryRecode(string s, out string? result)
  {
    byte[]? preresult = null;
    result = null;

    try
    {
      preresult = DEF_LAT_ENC.Value.GetBytes(s.ToCharArray());
    }
    catch (EncoderFallbackException e)
    {
      return false;
    }

    result = DEF_CYR_ENC.Value.GetString(preresult);
    return true;
  }
  public static string GetString(this XmlNode node, string s) =>
          node.Attributes.GetNamedItem(s)?.InnerText;
  /// <summary>
  /// Получить плавающее число из КсМЛ-узла
  /// </summary>
  /// <param name="node">Узел</param>
  /// <param name="s">Тип строки</param>
  /// <returns>Результат</returns>
  public static float GetFloat(this XmlNode node, string s) =>
    Convert.ToSingle(node.GetString(s), CultureInfo.InvariantCulture);
  /// <summary>
  /// Получить строку из элемента КсСЛ
  /// </summary>
  /// <param name="root">Элемент</param>
  /// <param name="s">Тип строки</param>
  /// <returns>Результат</returns>
  public static string? GetInnerString(this XmlElement root, string s) =>
    root?.GetElementsByTagName(s)?.Item(0)?.InnerText;

  public static string? RepairString(string s)
  {
    if (String.IsNullOrWhiteSpace(s)) return s;

    if (s[0] == '<' && s[s.Length - 1] == '>' && s.Length % 2 == 0)
      return RepairHexString(s);
    else if (s.Contains('\\') && _isAllAscii(s))
    {
      try { return _repairOctalString(s); }
      catch (FormatException e) { return s; }
    }
    else if(_isAllPseudoLatin1(s))
    {
      if(TryRecode(s, out string? preresult))
        return preresult;
    }

    return s;
  }
  /// <summary>
  /// Восстановить строку из гекс-записи
  /// </summary>
  /// <param name="s">Строка с гекс-записью</param>
  /// <returns>Результат</returns>
  public static string RepairHexString(string s)
  {
    if (s == null || s.Length < 3 || s[0] != '<' || s[s.Length - 1] != '>' || s.Length % 2 != 0)
      return s;
    byte[] newLine = new byte[(s.Length - 2) / 2];
    for (int i = 1; i < s.Length - 1; i++)
    {
      newLine[i / 2] = Convert.ToByte(s.Substring(i, 2), 16);
      i++;
    }
    return Encoding.GetEncoding(1251).GetString(newLine);
  }
  /// <summary>
  /// Восстанавливает строку из восьмиричной записи
  /// </summary>
  /// <param name="s">Строка с восьмиричной записью</param>
  /// <returns>Результат</returns>
  private static string _repairOctalString(string s)
  {
    int inputLength = s.Length;
    var output = new List<byte>(inputLength);

    for (int i = 0; i < inputLength; i++)
    {
      if (s[i] == '\\')
      {
        if (inputLength >= i + 3 && s[i + 1]._isOctalDigit() && s[i + 2]._isOctalDigit()
          && s[i + 3]._isOctalDigit())
        {
          var x = Convert.ToByte(s.Substring(i + 1, 3), 8);
          output.Add(x);
          i = i + 3;
        }
        else throw new FormatException();
      }
      else
        output.Add((byte)s[i]);
    }
    return Encoding.GetEncoding(DEF_WIN_CYR).GetString(output.ToArray());
  }
  /// <summary>
  /// Предохранить от использования спецсимволов ХТМЛ
  /// </summary>
  /// <param name="s"></param>
  /// <returns>Результат</returns>
  public static string Preserve(string sb) =>
    sb.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
  /// <summary>
  /// Получить последний символ из строки
  /// </summary>
  /// <param name="s">Строка</param>
  /// <returns>Последний символ</returns>
  public static char LastChar(this string s)
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
  public static char PenultChar(this string s)
  {
    int i = s.Length;
    char Char = i > 1 ? s[i - 2] : LastChar(s);
    return Char;
  }
  /// <summary>
  /// Получить строку без последнего символа
  /// </summary>
  /// <param name="s">Строка</param>
  /// <returns>Строка без последнего символа</returns>
  public static string WOLast(this string s) => s.Substring(0, s.Length - 1);
  /// <summary>
  /// Удаляет лишние пробелы у дефисов
  /// </summary>
  /// <param name="s1">Первая строка</param>
  /// <param name="s2">Вторая строка</param>
  /// <param name="dash">Символ дефиса</param>
  public static void DeleteDashes(ref string s1, ref string s2, char dash)
  {
    if (s1.Length > 1 && LastChar(s1) == dash && PenultChar(s1) != ' ')
      RenewStrings(ref s1, ref s2);
  }
  /// <summary>
  /// Удаляет переносы из строк
  /// </summary>
  /// <param name="s1">Первая строка</param>
  /// <param name="s2">Вторая строка</param>
  /// <param name="spell">Проверка правописания</param>
  public static void DeleteHyphens(ref string s1, ref string s2, WordList spell)
  {
    if (Program.hyphens && s1.Length > 1 && LastChar(s1) == '-' &&
      PenultChar(s1) != ' ' && PenultChar(s1) != '»')
    {
      var x = _endWord.Match(s1);
      var y = _firstWord.Match(s2);

      if (x.Length < 1 || y.Length < 1) return;

      char xp = x.ToString().PenultChar();
      if (Char.IsNumber(y.ToString()[0]) || Char.IsNumber(xp) || Char.IsUpper(s2[0])
        || xp == '.')
      {
        RenewStrings(ref s1, ref s2);
        return;
      }

      var z = $"{x.ToString().WOLast()}{y}";
      if (spell.Check(z))
      {
        s1 = s1.WOLast();
        RenewStrings(ref s1, ref s2);
        return;
      }

      /*if (spell.Check($"{x}{y}"))
        RenewStrings(ref s1, ref s2);*/
    }
  }
  /// <summary>
  /// Обновить строки
  /// </summary>
  /// <param name="s1">Первая строка</param>
  /// <param name="s2">Вторая строка</param>
  public static void RenewStrings(ref string s1, ref string s2)
  {
    int space = s2.IndexOf(' ');
    string rtHalfWord;
    if (space >= 0)
    {
      rtHalfWord = s2.Substring(0, space);
      s2 = s2.Substring(space + 1);
    }
    else
    {
      rtHalfWord = s2;
      s2 = "";
    }
    s1 = $"{s1}{rtHalfWord}";
  }
}