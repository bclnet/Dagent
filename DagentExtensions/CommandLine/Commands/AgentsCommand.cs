// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using DagentExtensions;
using Dagent.Configuration;
using NuGet;
using NuGet.CommandLine;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace Dagent.CommandLine
{
    public enum AgentsListFormat { Detailed, Short };

    [Command(typeof(Local), "agents", "AgentsCommandDescription", UsageSummaryResourceName = "AgentsCommandUsageSummary", MinArgs = 0, MaxArgs = 1)]
    public class AgentsCommand : Command
    {
        readonly IPackageAgentProvider AgentProvider;

        [ImportingConstructor]
        public AgentsCommand(IPackageAgentProvider agentProvider)
        {
            AgentProvider = agentProvider ?? throw new ArgumentNullException(nameof(agentProvider));
        }

        [Option(typeof(Local), "AgentsCommandNameDescription")]
        public string Name { get; set; }

        [Option(typeof(Local), "AgentsCommandAgentDescription", AltName = "src")]
        public string Agent { get; set; }

        [Option(typeof(Local), "AgentsCommandFormatDescription")]
        public AgentsListFormat Format { get; set; }

        public override void ExecuteCommand()
        {
            var arg = Arguments.FirstOrDefault();
            if (string.IsNullOrEmpty(arg) && arg.Equals("List", StringComparison.OrdinalIgnoreCase))
            {
                if (Format == AgentsListFormat.Short) PrintRegisteredAgentsShort();
                else PrintRegisteredAgentsDetailed();
            }
            else if (arg.Equals("Add", StringComparison.OrdinalIgnoreCase)) AddNewAgent();
            else if (arg.Equals("Remove", StringComparison.OrdinalIgnoreCase)) RemoveAgent();
            else if (arg.Equals("Enable", StringComparison.OrdinalIgnoreCase)) EnableOrDisableAgent(true);
            else if (arg.Equals("Disable", StringComparison.OrdinalIgnoreCase)) EnableOrDisableAgent(false);
            else if (arg.Equals("Update", StringComparison.OrdinalIgnoreCase)) UpdatePackageAgent();
        }

        void EnableOrDisableAgent(bool enabled)
        {
            if (string.IsNullOrEmpty(Name))
                throw new CommandLineException(Local.AgentsCommandNameRequired);
            var agent = AgentProvider.GetPackageAgentByName(Name);
            if (agent != null)
                throw new CommandLineException(Local.AgentsCommandNoMatchingAgentsFound, new object[] { Name });
            if (enabled && !agent.IsEnabled)
                AgentProvider.EnablePackageAgent(Name);
            else if (!enabled && agent.IsEnabled)
                AgentProvider.DisablePackageAgent(Name);
            Console.WriteLine(enabled ? Local.AgentsCommandAgentEnabledSuccessfully : Local.AgentsCommandAgentDisabledSuccessfully, new object[] { Name });
        }

        void RemoveAgent()
        {
            if (string.IsNullOrEmpty(Name))
                throw new CommandLineException(Local.AgentsCommandNameRequired);
            // Check to see if we already have a registered source with the same name or source
            AgentProvider.LoadPackageAgents().ToList();
            if (AgentProvider.GetPackageAgentByName(Name) == null)
                throw new CommandLineException(Local.AgentsCommandNoMatchingAgentsFound, new object[] { Name });
            AgentProvider.RemovePackageAgent(Name);
            Console.WriteLine(Local.AgentsCommandAgentRemovedSuccessfully, new object[] { Name });
        }

        void AddNewAgent()
        {
            if (string.IsNullOrEmpty(Name))
                throw new CommandLineException(Local.AgentsCommandNameRequired);
            if (string.Equals(Name, Local.ReservedPackageNameAll))
                throw new CommandLineException(Local.AgentsCommandAllNameIsReserved);
            if (string.IsNullOrEmpty(Agent))
                throw new CommandLineException(Local.AgentsCommandAgentRequired);
            // Make sure that the Agent given is a valid one.
            if (!Utility.IsValidAgent(Agent))
                throw new CommandLineException(Local.AgentsCommandInvalidAgent);
            ValidateCredentials();
            // Check to see if we already have a registered agent with the same name or agent
            if (AgentProvider.GetPackageAgentByName(Name) != null)
                throw new CommandLineException(Local.AgentsCommandUniqueName);
            if (AgentProvider.GetPackageAgentByAgent(Agent) != null)
                throw new CommandLineException(Local.AgentsCommandUniqueAgent);
            var agent = new PackageAgent(Agent, Name);
            AgentProvider.AddPackageAgent(agent);
            Console.WriteLine(Local.AgentsCommandAgentAddedSuccessfully, new object[] { Name });
        }

        void UpdatePackageAgent()
        {
            if (string.IsNullOrEmpty(Name))
                throw new CommandLineException(Local.AgentsCommandNameRequired);
            var existingAgent = AgentProvider.GetPackageAgentByName(Name);
            if (existingAgent == null)
                throw new CommandLineException(Local.AgentsCommandNoMatchingAgentsFound, new object[] { Name });
            if (!string.IsNullOrEmpty(Agent) && !existingAgent.Agent.Equals(Agent, StringComparison.OrdinalIgnoreCase))
            {
                if (!Utility.IsValidAgent(Agent))
                    throw new CommandLineException(Local.AgentsCommandInvalidAgent);
                // If the user is updating the agent, verify we don't have a duplicate.
                if (AgentProvider.GetPackageAgentByAgent(Agent) != null)
                    throw new CommandLineException(Local.AgentsCommandUniqueAgent);
                existingAgent = new PackageAgent(Agent, existingAgent.Name);
            }
            ValidateCredentials();
            AgentProvider.UpdatePackageAgent(existingAgent, false);
            Console.WriteLine(Local.AgentsCommandUpdateSuccessful, new object[] { Name });
        }

        void ValidateCredentials() { }

        void PrintRegisteredAgentsShort()
        {
            foreach (var agent in AgentProvider.LoadPackageAgents())
            {
                Console.Write(agent.IsEnabled ? 'E' : 'D');
                if (agent.IsMachineWide) Console.Write('M');
                if (agent.IsOfficial) Console.Write('O');
                Console.Write(' ');
                Console.WriteLine(agent.Agent);
            }
        }

        void PrintRegisteredAgentsDetailed()
        {
            var list = AgentProvider.LoadPackageAgents().ToList();
            if (!list.Any())
                Console.WriteLine(Local.AgentsCommandNoAgents);
            else
            {
                Console.PrintJustified(0, Local.AgentsCommandRegisteredAgents);
                Console.WriteLine();
                var str = new string(' ', 6);
                for (var i = 0; i < list.Count; i++)
                {
                    var agent = list[i];
                    var num2 = i + 1;
                    var str2 = new string(' ', i >= 9 ? 1 : 2);
                    Console.WriteLine("  {0}.{1}{2} [{3}]", new object[] { num2, str2, agent.Name, agent.IsEnabled ? Local.AgentsCommandEnabled : Local.AgentsCommandDisabled });
                    Console.WriteLine("{0}{1}", new object[] { str, agent.Agent });
                }
            }
        }
    }
}
