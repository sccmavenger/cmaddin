using System;
using System.Collections.Generic;
using System.Linq;
using ZeroTrustMigrationAddin.Models;

namespace ZeroTrustMigrationAddin.Services.AgentTools
{
    /// <summary>
    /// Registry of all available agent tools
    /// </summary>
    public class AgentToolkit
    {
        private readonly Dictionary<string, AgentTool> _tools = new();

        public AgentToolkit()
        {
        }

        public void RegisterTool(AgentTool tool)
        {
            _tools[tool.Name] = tool;
        }

        public AgentTool? GetTool(string name)
        {
            return _tools.GetValueOrDefault(name);
        }

        public List<AgentTool> GetAllTools()
        {
            return _tools.Values.ToList();
        }

        public List<object> GetFunctionDefinitions()
        {
            return _tools.Values
                .Select(t => t.ToFunctionDefinition())
                .ToList();
        }

        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }
    }
}
