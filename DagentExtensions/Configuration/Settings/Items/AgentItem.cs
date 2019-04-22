// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Configuration;
using System;

namespace Dagent.Configuration
{
    public class SettingsFile
    {
        public SettingsFile(SettingBase s)
        {
        }

        public int Priority { get; }
        public bool IsMachineWide { get; }

        internal void AddOrUpdate(SettingsFile origin, string disabledPackageAgents, AddItem addItem)
        {
        }
    }

    public sealed class AgentItem : AddItem
    {
        public readonly SettingsFile Origin;

        public string ProtocolVersion
        {
            get => MutableAttributes.TryGetValue(ConfigurationConstants.ProtocolVersionAttribute, out var attribute) ? Settings.ApplyEnvironmentTransform(attribute) : null;
            set => AddOrUpdateAttribute(ConfigurationConstants.ProtocolVersionAttribute, value);
        }

        public AgentItem(string key, string value, string protocolVersion = "")
            : base(key, value)
        {
            Origin = null; // new SettingsFile(this);
            if (!string.IsNullOrEmpty(protocolVersion))
                ProtocolVersion = protocolVersion;
        }

        //public override int GetHashCode()
        //{
        //    var combiner = new HashCodeCombiner();
        //    combiner.AddObject(Key);
        //    if (ProtocolVersion != null)
        //        combiner.AddObject(ProtocolVersion);
        //    return combiner.CombinedHash;
        //}

        public override SettingBase Clone()
        {
            var newSetting = new AgentItem(Key, Value, ProtocolVersion);
            //if (Origin != null)
            //    newSetting.SetOrigin(Origin);
            return newSetting;
        }

        public override bool Equals(object other)
        {
            if (!(other is SourceItem source))
                return false;
            if (ReferenceEquals(this, source))
                return true;
            return string.Equals(Key, source.Key, StringComparison.Ordinal) &&
                string.Equals(ProtocolVersion, source.ProtocolVersion, StringComparison.Ordinal);
        }
    }
}