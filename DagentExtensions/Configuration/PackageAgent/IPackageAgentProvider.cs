// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Dagent.Configuration
{
    public interface IPackageAgentProvider
    {
        /// <summary>
        /// Gets an enumerable of all of the package agents
        /// </summary>
        /// <returns>Enumerable of all of the package agents</returns>
        IEnumerable<PackageAgent> LoadPackageAgents();

        /// <summary>
        /// Gets the agent that matches a given name.
        /// </summary>
        /// <param name="name">Name of agent to be searched for</param>
        /// <returns>PackageAgent that matches the given name. Null if none was found</returns>
        /// <throws>ArgumentException when <paramref name="name"/> is null or empty.</throws>
        PackageAgent GetPackageAgentByName(string name);
        

        /// <summary>
        /// Gets the agent that matches a given agent url.
        /// </summary>
        /// <param name="agent">Url of agent to be searched for</param>
        /// <returns>PackageSource that matches the given agent. Null if none was found</returns>
        /// <throws>ArgumentException when <paramref name="agent"/> is null or empty.</throws>
        PackageAgent GetPackageAgentByAgent(string agent);

        /// <summary>
        /// Event raised when the package sources have been changed.
        /// </summary>
        event EventHandler PackageAgentsChanged;

        /// <summary>
        /// Removes the package agent that matches the given name
        /// </summary>
        /// <param name="name">Name of agent to remove</param>
        void RemovePackageAgent(string name);

        /// <summary>
        /// Enables the package source that matches the given name
        /// </summary>
        /// <param name="name">Name of agent to enable</param>
        void EnablePackageAgent(string name);

        /// <summary>
        /// Disables the package agent that matches the given name
        /// </summary>
        /// <param name="name">Name of agent to disable</param>
        void DisablePackageAgent(string name);

        /// <summary>
        /// Updates the values of the given package agent.
        /// </summary>
        /// <remarks>The package agent is matched by name.</remarks>
        /// <param name="agent">Agent with updated values</param>
        /// <param name="updateEnabled">Describes if enabled value from <paramref name="agent"/> should be updated or ignored</param>
        void UpdatePackageAgent(PackageAgent agent, bool updateEnabled);

        /// <summary>
        /// Adds a package agent to the current configuration
        /// </summary>
        /// <param name="agent">PackageAgent to add</param>
        void AddPackageAgent(PackageAgent agent);

        /// <summary>
        /// Compares the given list of PackageAgents with the current PackageAgents in the configuration
        /// and adds, removes or updates each agent as needed.
        /// </summary>
        /// <param name="sources">PackageAgents to be saved</param>
        void SavePackageAgents(IEnumerable<PackageAgent> agents);

        /// <summary>
        /// Checks if a package agent with a given name is part of the disabled ageets configuration
        /// </summary>
        /// <param name="name">Name of the agent to be queried</param>
        /// <returns>true if the agent with the given name is not part of the disabled agents</returns>
        bool IsPackageAgentEnabled(string name);

        /// <summary>
        /// Gets the name of the active PackageAgent
        /// </summary>
        string ActivePackageAgentName { get; }

        /// <summary>
        /// Gets the Default deploy agent
        /// </summary>
        string DefaultDeployAgent { get; }

        /// <summary>
        /// Updates the active package agent with the given agent.
        /// </summary>
        /// <param name="agent">Agent to be set as the active package agent</param>
        void SaveActivePackageAgent(PackageAgent agent);
    }
}
