// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;

namespace Dagent.RemoteAgent
{
    public interface IBusDispacher
    {
        void Send(string agent, object message);
    }
}
