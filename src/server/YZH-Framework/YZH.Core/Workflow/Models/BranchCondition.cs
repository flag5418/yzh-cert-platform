using System.Text.Json;
using System.Text.Json.Serialization;

namespace YZH.Core.Workflow.Models
{
    public enum BranchOp { Equals, NotEquals, Gt, Gte, Lt, Lte, Truthy }

    public class BranchCondition
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;
        [JsonPropertyName("op")]
        public BranchOp Op { get; set; }
        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }

    public class BranchOpConverter : JsonConverter<BranchOp>
    {
        public override BranchOp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString()?.ToLowerInvariant();
            return str switch
            {
                "equals" => BranchOp.Equals,
                "not_equals" => BranchOp.NotEquals,
                "gt" => BranchOp.Gt,
                "gte" => BranchOp.Gte,
                "lt" => BranchOp.Lt,
                "lte" => BranchOp.Lte,
                "truthy" => BranchOp.Truthy,
                _ => throw new JsonException($"未知 BranchOp: {str}")
            };
        }

        public override void Write(Utf8JsonWriter writer, BranchOp value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}
