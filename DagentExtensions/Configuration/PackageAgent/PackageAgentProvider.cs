// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Dagent.Configuration
{
    [Export(typeof(IPackageAgentProvider))]
    public class PackageAgentProvider : IPackageAgentProvider
    {
        public ISettings Settings { get; private set; }

        internal const int MaxSupportedProtocolVersion = 3;
        readonly IEnumerable<PackageAgent> _configurationDefaultAgents;

        [ImportingConstructor]
        public PackageAgentProvider(IMachineWideSettings settings) : this(settings.Settings) { }
        public PackageAgentProvider(ISettings settings) : this(settings, ConfigurationDefaults2.Instance.DefaultPackageAgents) { }
        public PackageAgentProvider(ISettings settings, IEnumerable<PackageAgent> configurationDefaultAgents)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.SettingsChanged += (_, __) => { OnPackageAgentsChanged(); };
            _configurationDefaultAgents = LoadConfigurationDefaultAgents(configurationDefaultAgents);
        }

        public event EventHandler PackageAgentsChanged;

        IEnumerable<PackageAgent> LoadConfigurationDefaultAgents(IEnumerable<PackageAgent> configurationDefaultAgents)
        {
            var packageAgentLookup = new Dictionary<string, IndexedPackageAgent>(StringComparer.OrdinalIgnoreCase);
            var packageIndex = 0;

            foreach (var packageAgent in configurationDefaultAgents)
                packageIndex = AddOrUpdateIndexedAgent(packageAgentLookup, packageIndex, packageAgent, packageAgent.Name);

            return packageAgentLookup.Values
                .OrderBy(agent => agent.Index)
                .Select(agent => agent.PackageAgent);
        }

        Dictionary<string, IndexedPackageAgent> LoadPackageAgentLookup(bool byName)
        {
            var packageAgentsSection = Settings.GetSection(ConfigurationConstants2.PackageAgents);
            var agentsItems = packageAgentsSection?.Items.OfType<AgentItem>();

            // Order the list so that the closer to the user appear first
            var agents = agentsItems?.OrderByDescending(item => item.Origin?.Priority ?? 0);

            // get list of disabled packages
            var disabledAgentsSection = Settings.GetSection(ConfigurationConstants2.DisabledPackageAgents);
            var disabledAgentsSettings = disabledAgentsSection?.Items.OfType<AddItem>();

            var disabledAgents = new HashSet<string>(disabledAgentsSettings?.GroupBy(setting => setting.Key).Select(group => group.First().Key) ?? Enumerable.Empty<string>());
            var packageAgentLookup = new Dictionary<string, IndexedPackageAgent>(StringComparer.OrdinalIgnoreCase);

            if (agents != null)
            {
                var packageIndex = 0;
                foreach (var setting in agents)
                {
                    var name = setting.Key;
                    var isEnabled = !disabledAgents.Contains(name);
                    var packageAgent = ReadPackageAgent(setting, isEnabled);

                    packageIndex = AddOrUpdateIndexedAgent(packageAgentLookup, packageIndex, packageAgent, byName ? packageAgent.Name : packageAgent.Agent);
                }
            }
            return packageAgentLookup;
        }

        Dictionary<string, IndexedPackageAgent> LoadPackageAgentLookupByName() => LoadPackageAgentLookup(byName: true);

        Dictionary<string, IndexedPackageAgent> LoadPackageAgentLookupByAgent() => LoadPackageAgentLookup(byName: false);

        /// <summary>
        /// Returns PackageAgents if specified in the config file. Else returns the default agents specified in the
        /// constructor.
        /// If no default values were specified, returns an empty sequence.
        /// </summary>
        public IEnumerable<PackageAgent> LoadPackageAgents()
        {
            var loadedPackageAgents = LoadPackageAgentLookupByName().Values
                .OrderBy(agent => agent.Index)
                .Select(agent => agent.PackageAgent)
                .ToList();
            if (_configurationDefaultAgents != null && _configurationDefaultAgents.Any())
                SetDefaultPackageAgents(loadedPackageAgents);
            return loadedPackageAgents;
        }

        void SetDefaultPackageAgents(List<PackageAgent> loadedPackageAgents)
        {
            var defaultPackageAgentsToBeAdded = new List<PackageAgent>();

            foreach (var packageAgent in _configurationDefaultAgents)
            {
                var agentMatching = loadedPackageAgents.Any(p => p.Agent.Equals(packageAgent.Agent, StringComparison.CurrentCultureIgnoreCase));
                var feedNameMatching = loadedPackageAgents.Any(p => p.Name.Equals(packageAgent.Name, StringComparison.CurrentCultureIgnoreCase));

                if (!agentMatching && !feedNameMatching)
                    defaultPackageAgentsToBeAdded.Add(packageAgent);
            }

            var defaultAgentsInsertIndex = loadedPackageAgents.FindIndex(agent => agent.IsMachineWide);

            if (defaultAgentsInsertIndex == -1)
                defaultAgentsInsertIndex = loadedPackageAgents.Count;

            loadedPackageAgents.InsertRange(defaultAgentsInsertIndex, defaultPackageAgentsToBeAdded);
        }

        PackageAgent ReadPackageAgent(AgentItem setting, bool isEnabled)
        {
            var name = setting.Key;
            var packageAgent = new PackageAgent(setting.GetValueAsPath(), name, isEnabled)
            {
                IsMachineWide = setting.Origin?.IsMachineWide ?? false,
            };
            return packageAgent;
        }

        static int ReadProtocolVersion(AgentItem setting) => int.TryParse(setting.ProtocolVersion, out var protocolVersion) ? protocolVersion : PackageSource.DefaultProtocolVersion;

        static int AddOrUpdateIndexedAgent(Dictionary<string, IndexedPackageAgent> packageAgentLookup, int packageIndex, PackageAgent packageAgent, string lookupKey)
        {
            if (!packageAgentLookup.TryGetValue(lookupKey, out var previouslyAddedAgent))
                packageAgentLookup[lookupKey] = new IndexedPackageAgent
                {
                    PackageAgent = packageAgent,
                    Index = packageIndex++
                };
            else if (previouslyAddedAgent.PackageAgent.ProtocolVersion < packageAgent.ProtocolVersion && packageAgent.ProtocolVersion <= MaxSupportedProtocolVersion)
                // Pick the package agent with the highest supported protocol version
                previouslyAddedAgent.PackageAgent = packageAgent;

            return packageIndex;
        }

        PackageAgent GetPackageAgent(string key, Dictionary<string, IndexedPackageAgent> agentsLookup)
        {
            if (agentsLookup.TryGetValue(key, out var indexedPackageAgent))
                return indexedPackageAgent.PackageAgent;

            if (_configurationDefaultAgents != null && _configurationDefaultAgents.Any())
            {
                var loadedPackageAgents = agentsLookup.Values
                    .OrderBy(agent => agent.Index)
                    .Select(agent => agent.PackageAgent)
                    .ToList();

                foreach (var packageAgent in _configurationDefaultAgents)
                {
                    var isAgentMatch = loadedPackageAgents.Any(p => p.Agent.Equals(packageAgent.Agent, StringComparison.CurrentCultureIgnoreCase));
                    var isFeedNameMatch = loadedPackageAgents.Any(p => p.Name.Equals(packageAgent.Name, StringComparison.CurrentCultureIgnoreCase));

                    if (isAgentMatch || isFeedNameMatch)
                        return packageAgent;
                }
            }

            return null;
        }

        public PackageAgent GetPackageAgentByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", nameof(name));
            return GetPackageAgent(name, LoadPackageAgentLookupByName());
        }

        public PackageAgent GetPackageAgentByAgent(string agent)
        {
            if (string.IsNullOrEmpty(agent))
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", nameof(agent));
            return GetPackageAgent(agent, LoadPackageAgentLookupByAgent());
        }

        public void RemovePackageAgent(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", nameof(name));
            var isDirty = false;
            RemovePackageAgent(name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        void RemovePackageAgent(string name, bool shouldSkipSave, ref bool isDirty)
        {
            // get list of agents
            var packageAgentsSection = Settings.GetSection(ConfigurationConstants2.PackageAgents);
            var agentsSettings = packageAgentsSection?.Items.OfType<AgentItem>();

            var agentsToRemove = agentsSettings?.Where(s => string.Equals(s.Key, name, StringComparison.OrdinalIgnoreCase));

            if (agentsToRemove != null)
                foreach (var agent in agentsToRemove)
                    try
                    {
                        Settings.Remove(ConfigurationConstants2.PackageAgents, agent);
                        isDirty = true;
                    }
                    catch { }

            RemoveDisabledAgent(name, shouldSkipSave: true, isDirty: ref isDirty);

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageAgentsChanged();
                isDirty = false;
            }
        }

        public void DisablePackageAgent(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", nameof(name));
            var isDirty = false;
            AddDisabledAgent(name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        void AddDisabledAgent(string name, bool shouldSkipSave, ref bool isDirty)
        {
            var settingsLookup = GetExistingSettingsLookup();
            var addedInSameFileAsCurrentAgent = false;

            if (settingsLookup.TryGetValue(name, out var agentSetting))
                try
                {
                    if (agentSetting.Origin != null)
                    {
                        agentSetting.Origin.AddOrUpdate(agentSetting.Origin, ConfigurationConstants2.DisabledPackageAgents, new AddItem(name, "true"));
                        isDirty = true;
                        addedInSameFileAsCurrentAgent = true;
                    }
                }
                // We ignore any errors since this means the current agent file could not be edited
                catch { }

            if (!addedInSameFileAsCurrentAgent)
            {
                Settings.AddOrUpdate(ConfigurationConstants2.DisabledPackageAgents, new AddItem(name, "true"));
                isDirty = true;
            }

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageAgentsChanged();
                isDirty = false;
            }
        }

        public void EnablePackageAgent(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", nameof(name));
            var isDirty = false;
            RemoveDisabledAgent(name, shouldSkipSave: false, isDirty: ref isDirty);
        }

        void RemoveDisabledAgent(string name, bool shouldSkipSave, ref bool isDirty)
        {
            // get list of disabled agents
            var disabledAgentsSection = Settings.GetSection(ConfigurationConstants2.DisabledPackageAgents);
            var disabledAgentsSettings = disabledAgentsSection?.Items.OfType<AddItem>();

            var disableAgentsToRemove = disabledAgentsSettings?.Where(s => string.Equals(s.Key, name, StringComparison.OrdinalIgnoreCase));

            if (disableAgentsToRemove != null)
                foreach (var disabledAgent in disableAgentsToRemove)
                {
                    Settings.Remove(ConfigurationConstants2.DisabledPackageAgents, disabledAgent);
                    isDirty = true;
                }

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageAgentsChanged();
                isDirty = false;
            }
        }

        public void UpdatePackageAgent(PackageAgent agent, bool updateEnabled)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));

            var packageAgents = GetExistingSettingsLookup();
            packageAgents.TryGetValue(agent.Name, out var agentToUpdate);

            if (agentToUpdate != null)
            {
                AddItem disabledAgentItem = null;

                if (updateEnabled)
                {
                    // get list of disabled packages
                    var disabledAgentsSection = Settings.GetSection(ConfigurationConstants2.DisabledPackageAgents);
                    disabledAgentItem = disabledAgentsSection?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, agentToUpdate.ElementName);
                }

                var oldPackageAgent = ReadPackageAgent(agentToUpdate, disabledAgentItem == null);
                var isDirty = false;

                UpdatePackageAgent(
                    agent,
                    oldPackageAgent,
                    disabledAgentItem,
                    updateEnabled,
                    shouldSkipSave: false,
                    isDirty: ref isDirty);
            }
        }

        void UpdatePackageAgent(
            PackageAgent newAgent,
            PackageAgent existingAgent,
            AddItem existingDisabledAgentItem,
            bool updateEnabled,
            bool shouldSkipSave,
            ref bool isDirty)
        {
            if (string.Equals(newAgent.Name, existingAgent.Name, StringComparison.OrdinalIgnoreCase))
            {
                if ((!string.Equals(newAgent.Agent, existingAgent.Agent, StringComparison.OrdinalIgnoreCase) ||
                    newAgent.ProtocolVersion != existingAgent.ProtocolVersion) && newAgent.IsPersistable)
                {
                    Settings.AddOrUpdate(ConfigurationConstants2.PackageAgents, newAgent.AsAgentItem());
                    isDirty = true;
                }

                if (updateEnabled)
                {
                    if (newAgent.IsEnabled && existingDisabledAgentItem != null)
                    {
                        Settings.Remove(ConfigurationConstants2.DisabledPackageAgents, existingDisabledAgentItem);
                        isDirty = true;
                    }

                    if (!newAgent.IsEnabled && existingDisabledAgentItem == null)
                        AddDisabledAgent(newAgent.Name, shouldSkipSave: true, isDirty: ref isDirty);
                }

                if (!shouldSkipSave && isDirty)
                {
                    Settings.SaveToDisk();
                    OnPackageAgentsChanged();
                    isDirty = false;
                }
            }
        }

        public void AddPackageAgent(PackageAgent agent)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            var isDirty = false;
            AddPackageAgent(agent, shouldSkipSave: false, isDirty: ref isDirty);
        }

        void AddPackageAgent(PackageAgent agent, bool shouldSkipSave, ref bool isDirty)
        {
            if (agent.IsPersistable)
            {
                Settings.AddOrUpdate(ConfigurationConstants2.PackageAgents, agent.AsAgentItem());
                isDirty = true;
            }

            if (agent.IsEnabled)
                RemoveDisabledAgent(agent.Name, shouldSkipSave: true, isDirty: ref isDirty);
            else
                AddDisabledAgent(agent.Name, shouldSkipSave: true, isDirty: ref isDirty);

            if (!shouldSkipSave && isDirty)
            {
                Settings.SaveToDisk();
                OnPackageAgentsChanged();
                isDirty = false;
            }
        }

        public void SavePackageAgents(IEnumerable<PackageAgent> agents)
        {
            if (agents == null)
                throw new ArgumentNullException(nameof(agents));

            var isDirty = false;
            var existingSettingsLookup = GetExistingSettingsLookup();

            var disabledAgentsSection = Settings.GetSection(ConfigurationConstants2.DisabledPackageAgents);
            var existingDisabledAgents = disabledAgentsSection?.Items.OfType<AddItem>();
            var existingDisabledAgentsLookup = existingDisabledAgents?.ToDictionary(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var agent in agents)
            {
                AddItem existingDisabledAgentItem = null;
                AgentItem existingAgentItem = null;

                var existingAgentIsEnabled = existingDisabledAgentsLookup == null || existingDisabledAgentsLookup.TryGetValue(agent.Name, out existingDisabledAgentItem);

                if (existingSettingsLookup != null &&
                    existingSettingsLookup.TryGetValue(agent.Name, out existingAgentItem) &&
                    ReadProtocolVersion(existingAgentItem) == agent.ProtocolVersion)
                {
                    var oldPackageAgent = ReadPackageAgent(existingAgentItem, existingAgentIsEnabled);

                    UpdatePackageAgent(
                        agent,
                        oldPackageAgent,
                        existingDisabledAgentItem,
                        updateEnabled: true,
                        shouldSkipSave: true,
                        isDirty: ref isDirty);
                }
                else
                    AddPackageAgent(agent, shouldSkipSave: true, isDirty: ref isDirty);

                if (existingAgentItem != null)
                    existingSettingsLookup.Remove(agent.Name);
            }

            if (existingSettingsLookup != null)
            {
                foreach (var agentItem in existingSettingsLookup)
                {
                    if (existingDisabledAgentsLookup != null && existingDisabledAgentsLookup.TryGetValue(agentItem.Value.Key, out var existingDisabledAgentItem))
                    {
                        Settings.Remove(ConfigurationConstants2.DisabledPackageAgents, existingDisabledAgentItem);
                        isDirty = true;
                    }

                    Settings.Remove(ConfigurationConstants2.PackageAgents, agentItem.Value);
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                Settings.SaveToDisk();
                OnPackageAgentsChanged();
                isDirty = false;
            }
        }

        private Dictionary<string, AgentItem> GetExistingSettingsLookup()
        {
            var agentsSection = Settings.GetSection(ConfigurationConstants2.PackageAgents);
            var existingSettings = agentsSection?.Items.OfType<AgentItem>().Where(c => !c.Origin?.IsMachineWide ?? true).ToList();

            var existingSettingsLookup = new Dictionary<string, AgentItem>(StringComparer.OrdinalIgnoreCase);
            if (existingSettings != null)
                foreach (var setting in existingSettings)
                {
                    if (existingSettingsLookup.TryGetValue(setting.Key, out var previouslyAddedSetting) &&
                        ReadProtocolVersion(previouslyAddedSetting) < ReadProtocolVersion(setting) &&
                        ReadProtocolVersion(setting) <= MaxSupportedProtocolVersion)
                        existingSettingsLookup.Remove(setting.Key);
                    existingSettingsLookup[setting.Key] = setting;
                }
            return existingSettingsLookup;
        }

        /// <summary>
        /// Fires event PackageAgentsChanged
        /// </summary>
        void OnPackageAgentsChanged() => PackageAgentsChanged?.Invoke(this, EventArgs.Empty);

        public string DefaultPushAgent
        {
            get
            {
                var agent = SettingsUtility2.GetDefaultDeployAgent(Settings);

                if (string.IsNullOrEmpty(agent))
                    agent = ConfigurationDefaults2.Instance.DefaultDeployAgent;
                return agent;
            }
        }

        public bool IsPackageAgentEnabled(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", nameof(name));
            var disabledAgents = Settings.GetSection(ConfigurationConstants2.DisabledPackageAgents);
            var value = disabledAgents?.GetFirstItemWithAttribute<AddItem>(ConfigurationConstants.KeyAttribute, name);

            // It doesn't matter what value it is.
            // As long as the package agent name is persisted in the <disabledPackageAgents> section, the agent is disabled.
            return value == null;
        }

        /// <summary>
        /// Gets the name of the ActivePackageAgent from NuGet.Config
        /// </summary>
        public string ActivePackageAgentName
        {
            get
            {
                var activeAgentSection = Settings.GetSection(ConfigurationConstants2.ActivePackageAgentSectionName);
                return activeAgentSection?.Items.OfType<AddItem>().FirstOrDefault()?.Key;
            }
        }

        public string DefaultDeployAgent => throw new NotImplementedException();

        /// <summary>
        /// Saves the <paramref name="agent" /> as the active agent.
        /// </summary>
        /// <param name="agent"></param>
        public void SaveActivePackageAgent(PackageAgent agent)
        {
            try
            {
                var activePackageAgentSection = Settings.GetSection(ConfigurationConstants2.ActivePackageAgentSectionName);

                if (activePackageAgentSection != null)
                    foreach (var activePackageAgent in activePackageAgentSection.Items)
                        Settings.Remove(ConfigurationConstants2.ActivePackageAgentSectionName, activePackageAgent);

                Settings.AddOrUpdate(ConfigurationConstants2.ActivePackageAgentSectionName,
                        new AddItem(agent.Name, agent.Agent));

                Settings.SaveToDisk();
            }
            // we want to ignore all errors here.
            catch (Exception) { }
        }

        class IndexedPackageAgent
        {
            public int Index { get; set; }

            public PackageAgent PackageAgent { get; set; }
        }
    }
}