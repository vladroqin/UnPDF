using System.Text;

namespace UnPDF;

public static class MyTest
{
  public static void Main(string[] args)
  {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    var param = new CLParse(args);
    var work = new WorkWPdfs(param);
    work.WorkWFile();
  }
}