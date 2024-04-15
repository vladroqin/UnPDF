using System.Configuration;
using System.ComponentModel;
using Mono.Unix;
using Mono.Unix.Native;

namespace UnPDF;

public class CLParse(IReadOnlyList<string> args)
{
  #region Константы
  private const string HELP =
  """
  unpdf [-?hn-] inputFile [outputFile]
         -h  Help
         -?  Help
         -n  Disable dehyphenation
  """;
  private const string FONT_INFO = "fontinfo.txt";
  private const string FONT_MAP = "fontmap.txt";
  // https://stackoverflow.com/questions/17279712/what-is-the-smallest-possible-valid-pdf#answer-sort-dropdown-select-menu
  // private const long MINIMUM_SIZE = 67L;
  #endregion
  private UnPdfSettings _sets = new();
  private UnixFileInfo? _inputFile;
  /// <summary>
  /// Проверить входной файл
  /// </summary>
  /// <param name="s">Имя входного файла</param>
  private void CheckAndAddInputFile(string s)
  {
    if (!File.Exists(s))
      throw new Exception($"The file {s} doesn't exist!");
    if (!s.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase) &
        !s.EndsWith(".tetml", StringComparison.InvariantCultureIgnoreCase))
      throw new Exception("This isn't a PDF or TETML file!");

    _sets.InputFile = s;
    _inputFile = new UnixFileInfo(s);
    bool canRead = _inputFile.CanAccess(AccessModes.R_OK);
    if (!canRead)
      throw new Exception($"The file {s} cant't be read!");
    // С ссылками не работает как надо ☹
    // if (_sets.InputFile.Length < MINIMUM_SIZE)
    //   throw new Exception($"The file {s} too small!");
  }
  /// <summary>
  /// Проверить выходной файл
  /// </summary>
  /// <param name="s">Имя выходного файла</param>
  private void CheckAndAddOutputFile(string s)
  {
    if (File.Exists(s))
      throw new Exception("The file alredy exists!");
    if (!(_inputFile!.Directory).CanAccess(AccessModes.W_OK))
      throw new Exception("It's impossible to write in this directory!");
    _sets.OutputFile = s;
  }
  /// <summary>
  /// Загрузить всё остальное
  /// </summary>
  private void LoadingEverythingElse()
  {
    try
    {
      _sets.Aff = ConfigurationManager.AppSettings["AFF"]!;
      _sets.Dic = ConfigurationManager.AppSettings["DIC"]!;
    }
    catch
    {
      throw new Exception("Where's dictionary?");
    }

    if (File.Exists(FONT_INFO))
    {
      _sets.FontInfo = File.ReadLines(FONT_INFO);
    }

    if (File.Exists(FONT_MAP))
    {
      _sets.FontMap = File.ReadAllLines(FONT_MAP);
    }
  }
  /// <summary>
  /// Без параметров.
  /// </summary>
  private void Count0()
  {
    Console.WriteLine(HELP);
    Environment.Exit(0);
  }
  /// <summary>
  /// С одним параметром
  /// </summary>
  /// <exception cref="Exception"></exception> <summary>
  ///
  /// </summary>
  private void Count1()
  {
    if (args[0] == "-h" || args[0] == "-?")
    {
      Console.WriteLine(HELP);
      Environment.Exit(0);
    }

    if ((args[0] == "-n") ||
        (args[0] == "-") ||
        (args[0] == "--"))
    {
      throw new Exception("Where's the input file?");
    }

    CheckAndAddInputFile(args[0]);
    LoadingEverythingElse();

    string newFile = $"{Path.GetFileNameWithoutExtension(args[0])}.htm";
    CheckAndAddOutputFile(newFile);
  }
  /// <summary>
  /// С двумя параметрами
  /// </summary>
  private void Count2()
  {
    if (args[0][0] == '-')
    {
      if (args[0].Contains('?') ||
          args[0].Contains('h'))
      {
        Console.WriteLine(HELP);
        Environment.Exit(0);
      }
      if (args[0].Contains('n'))
      {
        _sets.EnableHyphens = false;
      }

      CheckAndAddInputFile(args[1]);
      LoadingEverythingElse();

      string newFile = $"{Path.GetFileNameWithoutExtension(args[1])}.htm";
      CheckAndAddOutputFile(newFile);
    }
    else
    {
      CheckAndAddInputFile(args[0]);
      LoadingEverythingElse();
      CheckAndAddOutputFile(args[1]);
    }
  }
  /// <summary>
  /// С тремя параметрами
  /// </summary>
  private void Count3()
  {
    if (args[0][0] == '-')
    {
      if (args[0].Contains('?') ||
          args[0].Contains('h'))
      {
        Console.WriteLine(HELP);
        Environment.Exit(0);
      }
      if (args[0].Contains('n'))
      {
        _sets.EnableHyphens = false;
      }
      CheckAndAddInputFile(args[1]);
      LoadingEverythingElse();
      CheckAndAddOutputFile(args[2]);
     }
     else
     {
      CheckAndAddInputFile(args[0]);
      LoadingEverythingElse();
      CheckAndAddOutputFile(args[1]);
     }
  }
  /// <summary>
  /// Разобрать и интерпретировать коммандную строку
  /// </summary>
  /// <param name="args">Элементы коммандной строки</param>
  /// <returns>Настройки</returns>
  public UnPdfSettings Parse()
  {
    try
    {
      Static.Command("mutool", String.Empty);
      Static.Command("tet", String.Empty);
    }
    catch (Win32Exception)
    {
      Static.PrintRed("Для работы нужны программы (прописанные в PATH) mutool и tet!");
      throw new FileNotFoundException();
    }

    if (args == null)
      throw new Exception("What the fuck?");

    int clcount = args.Count;

    try
    {
      switch (clcount)
      {
        case 0:
          Count0();
          break;
        case 1:
          Count1();
          break;
        case 2:
          Count2();
          break;
        default:
          Count3();
          break;
      }
    }
    catch (Exception e)
    {
      Static.PrintRed(e.Message);
      Environment.Exit(-1);
    };

    return _sets;
  }
}