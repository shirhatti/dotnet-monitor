﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Tools.Monitor
{
    internal sealed class UrlBindingChange
    {
        public string OriginalUrl { get; set; }

        public string NewUrl { get; set; }
    }
}
