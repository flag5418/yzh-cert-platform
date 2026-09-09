namespace YZH.Core.DataBase.Models;

/// <summary>
///     SQL 查询条件
/// </summary>
public class SqlCondition
{
    /// <summary>字段名</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>操作符（=, !=, &gt;, &gt;=, &lt;, &lt;=, LIKE, IN 等）</summary>
    public string Operator { get; set; } = "=";

    /// <summary>值</summary>
    public object? Value { get; set; }

    public SqlCondition() { }

    public SqlCondition(string field, string operatorValue, object? value = null)
    {
        Field = field;
        Operator = operatorValue;
        Value = value;
    }
}
