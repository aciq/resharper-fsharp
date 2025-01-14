using JetBrains.DocumentModel;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;

namespace JetBrains.ReSharper.Plugins.FSharp.Psi
{
  public interface IFSharpLanguageService
  {
    IFSharpParser CreateParser(IDocument document, IPsiSourceFile sourceFile, bool useFsExtension = false);
    IFSharpElementFactory CreateElementFactory(IPsiSourceFile sourceFile, IPsiModule psiModule);
  }
}
