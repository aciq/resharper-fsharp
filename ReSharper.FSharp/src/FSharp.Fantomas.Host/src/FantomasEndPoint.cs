﻿using JetBrains.Collections.Viewable;
using JetBrains.Diagnostics;
using JetBrains.Lifetimes;
using JetBrains.Platform.RdFramework.ExternalProcess;
using JetBrains.Platform.RdFramework.ExternalProcess.Util;
using JetBrains.Rd.Impl;
using JetBrains.Util;
using JetBrains.Rd.Tasks;
using JetBrains.ReSharper.Plugins.FSharp.Fantomas.Protocol;
using JetBrains.ReSharper.Plugins.FSharp.Fantomas.Server;

namespace JetBrains.ReSharper.Plugins.FSharp.Fantomas.Host
{
  internal class ExternalFormatterEndPoint : ProtocolEndPoint<RdFantomasModel, RdSimpleDispatcher>
  {
    private readonly FantomasCodeFormatter myCodeFormatter;
    protected override string ProtocolName => "External Formatter Host";

    public ExternalFormatterEndPoint() : base(FantomasProtocolConstants.PARENT_PROCESS_PID_ENV_VARIABLE)
    {
      myCodeFormatter = new FantomasCodeFormatter();
    }

    protected override RdSimpleDispatcher InitDispatcher(Lifetime lifetime, ILogger logger) =>
      new RdSimpleDispatcher(lifetime, logger);

    protected override void InitLogger(Lifetime lifetime, string path) =>
      ProtocolEndPointUtil.InitLogger(path, lifetime, LoggingLevel.TRACE);

    protected override RdFantomasModel InitModel(Lifetime lifetime, JetBrains.Rd.Impl.Protocol protocol)
    {
      var model = new RdFantomasModel(lifetime, protocol);

      model.FormatSelection.Set(FormatSelection);
      model.FormatDocument.Set(FormatDocument);
      model.Exit.Advise(lifetime, Terminate);

      return model;
    }

    private string FormatSelection(RdFormatSelectionArgs args) => myCodeFormatter.FormatSelection(args);
    private string FormatDocument(RdFormatDocumentArgs args) => myCodeFormatter.FormatDocument(args);

    protected override void Run(Lifetime lifetime, RdSimpleDispatcher dispatcher) => dispatcher.Run();
  }
}
