namespace YZH.Core.AI.Prompt.Models
{
    public class ParseResult<T> where T : class
    {
        public bool Success { get; set; }
        public T? Value { get; set; }
        public string? Error { get; set; }
        public string? RawText { get; set; }
    }
}
