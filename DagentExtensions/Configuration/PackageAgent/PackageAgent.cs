// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using System;

namespace Dagent.Configuration
{
    public class PackageAgent : IEquatable<PackageAgent>
    {
        /// <summary>
        /// The feed version for NuGet prior to v3.
        /// </summary>
        public const int DefaultProtocolVersion = 2;

        readonly int _hashCode;

        public string Name { get; }

        public string Agent { get; }

        public string DefaultEmail { get; }

        /// <summary>
        /// Returns null if Agent is an invalid URI
        /// </summary>
        public Uri TryAgentAsUri => UriUtility.TryCreateSourceUri(Agent, UriKind.Absolute);

        /// <summary>
        /// Throws if Agent is an invalid URI
        /// </summary>
        public Uri AgentUri => UriUtility.CreateSourceUri(Agent, UriKind.Absolute);

        /// <summary>
        /// This does not represent just the NuGet Official Feed alone
        /// It may also represent a Default Package Source set by Configuration Defaults
        /// </summary>
        public bool IsOfficial { get; set; }

        public bool IsMachineWide { get; set; }

        public bool IsEnabled { get; set; }

        public string Description { get; set; }

        public bool IsPersistable { get; private set; }

        /// <summary>
        /// Gets or sets the protocol version of the source. Defaults to 2.
        /// </summary>
        public int ProtocolVersion { get; set; } = DefaultProtocolVersion;

        public PackageAgent(string agent)
            : this(agent, agent, null, isEnabled: true, isOfficial: false) { }
        public PackageAgent(string agent, string name)
            : this(agent, name, null, isEnabled: true, isOfficial: false) { }
        public PackageAgent(string source, string name, bool isEnabled)
            : this(source, name, null, isEnabled, isOfficial: false) { }
        public PackageAgent(string agent, string name, string defaultEmail, bool isEnabled, bool isOfficial, bool isPersistable = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            DefaultEmail = defaultEmail;
            IsEnabled = isEnabled;
            IsOfficial = isOfficial;
            IsPersistable = isPersistable;
            _hashCode = (Name.ToUpperInvariant().GetHashCode() * 0xc41) + Agent.ToUpperInvariant().GetHashCode();
        }

        public AgentItem AsAgentItem() => new AgentItem(Name, Agent);

        public bool Equals(PackageAgent other) =>
            other == null ?
            false :
            Name.Equals(other.Name, StringComparison.CurrentCultureIgnoreCase) && Agent.Equals(other.Agent, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj)
        {
            var agent = obj as PackageAgent;
            return agent != null ? Equals(agent) : base.Equals(obj);
        }

        public override string ToString() => Name + " [" + Agent + "]";

        public override int GetHashCode() => _hashCode;

        public PackageAgent Clone() => new PackageAgent(Agent, Name, DefaultEmail, IsEnabled, IsOfficial, IsPersistable)
        {
            Description = Description,
            IsMachineWide = IsMachineWide,
        };
    }
}
