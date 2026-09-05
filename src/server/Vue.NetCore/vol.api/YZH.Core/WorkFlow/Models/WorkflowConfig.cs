using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YZH.Core.Workflow.Models
{
    public class WorkflowConfig
    {
        [JsonPropertyName("nodes")]
        public List<WorkflowNode> Nodes { get; set; } = new();

        [JsonPropertyName("edges")]
        public List<WorkflowEdge> Edges { get; set; } = new();

        [JsonPropertyName("branches")]
        public List<BranchConfig>? Branches { get; set; }

        [JsonPropertyName("output_config")]
        public Dictionary<string, object>? OutputConfig { get; set; }

        public IReadOnlyList<WorkflowNode> AllNodes()
        {
            var set = new List<WorkflowNode>(Nodes);
            var seen = new HashSet<string>(Nodes.Select(n => n.NodeId));
            foreach (var b in Branches ?? new())
                foreach (var n in b.Then)
                    if (seen.Add(n.NodeId)) set.Add(n);
            return set;
        }

        public (WorkflowNode Node, bool IsBranch, BranchConfig? Branch) FindNode(string nodeId)
        {
            foreach (var n in Nodes)
                if (n.NodeId == nodeId) return (n, false, null);
            foreach (var b in Branches ?? new())
                foreach (var n in b.Then)
                    if (n.NodeId == nodeId) return (n, true, b);
            throw new System.ArgumentException($"节点 {nodeId} 不存在");
        }
    }
}
