using System.Collections.Generic;

namespace YZH.Core.Workflow
{
    public class WorkflowContext
    {
        public string WorkflowInstanceId { get; set; } = string.Empty;
        public string BusinessType { get; set; } = "file_upload";
        public string BusinessCode { get; set; } = string.Empty;
        public long BusinessId { get; set; }
        public IDictionary<string, object> Inputs { get; set; } = new Dictionary<string, object>();
        public IExecutionLogStore? LogStore { get; set; }
    }
}
