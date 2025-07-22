// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;


namespace Cyclone_ESP32
{
    public class Program
    {
        public static void Main()
        {
            // App must not return.
            Thread.Sleep(Timeout.Infinite);
        }        
    }
}
