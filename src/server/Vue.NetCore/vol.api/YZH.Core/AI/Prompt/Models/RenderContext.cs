using System.Collections.Generic;

namespace YZH.Core.AI.Prompt.Models
{
    public class RenderContext : Dictionary<string, object>
    {
        public RenderContext() { }
        public RenderContext(IDictionary<string, object> map) : base(map) { }
    }
}
