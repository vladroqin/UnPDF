namespace UnPDF;

public class UnPdfSettings
{
#pragma warning disable CS8618
  public string InputFile { get; set; }
  public string OutputFile { get; set; }
  public string Aff { get; set; }
  public string Dic { get; set; }
  public IEnumerable<string>? FontInfo { get; set; }
  public IEnumerable<string>? FontMap { get; set; }
  public bool EnableHyphens { get; set; } = true;
#pragma warning restore CS8618
}