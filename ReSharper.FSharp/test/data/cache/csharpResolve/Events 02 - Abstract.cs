﻿using System;
using Microsoft.FSharp.Control;
using static Module;

public class Class1 : IFSharpInterface
{
  public event FSharpHandler<Exception> TheEvent;
}
