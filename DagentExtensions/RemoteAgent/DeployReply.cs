// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Dagent.RemoteAgent
{
    public class DeployReply
    {
        public class WaitState
        {
            internal ManualResetEvent WaitEvent = new ManualResetEvent(false);

            public Action<string> Success { get; set; }
            public Action Failure { get; set; }

            public void DoWait()
            {
                if (!WaitEvent.WaitOne(TimeSpan.FromSeconds(30), false))
                    Failure();
            }
        }

        public string Body { get; set; }
    }
}