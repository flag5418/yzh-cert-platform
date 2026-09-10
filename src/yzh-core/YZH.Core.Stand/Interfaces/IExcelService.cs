namespace YZH.Core.Stand.Interfaces;

/// <summary>
///     Excel 导出/导入服务接口（架构层约定）
///
///     职责：将实体列表导出为 Excel / 从 Excel 解析为实体列表
///
///     架构层只定义接口，具体实现由业务项目层提供（推荐 EPPlus / MiniExcel / NPOI）
///
///     为什么不在 YZH.Core 内置实现？
///     1. 业务项目（certplatform-api）可能选型不同的 Excel 库（EPPlus 商业许可 / NPOI 开源）
///     2. Excel 字段映射（哪些字段导出、是否脱敏、字典翻译）是业务相关的
///     3. 导入逻辑（行校验、错误回滚）通常与业务工作流耦合
///
///     注册方式（YzhWebBuilder.UseYzhCore 之后）：
///     services.AddSingleton&lt;IExcelService, EpplusExcelService&gt;();
///     或
///     services.AddSingleton&lt;IExcelService, NpoiExcelService&gt;();
///
///     业务 Controller 子类可直接通过构造注入 IExcelService override ExportCore/ImportCore
/// </summary>
public interface IExcelService
{
    /// <summary>
    ///     导出实体列表为 Excel 字节流
    /// </summary>
    /// <param name="entities">实体列表</param>
    /// <param name="columnMapping">列映射（FieldName → DesName），null 时反射所有 public 属性</param>
    /// <param name="sheetName">Sheet 名称</param>
    /// <returns>Excel 字节流</returns>
    byte[] ExportToExcel<T>(IList<T> entities, IDictionary<string, string>? columnMapping = null, string sheetName = "Sheet1") where T : class;

    /// <summary>
    ///     从 Excel 字节流解析为实体列表
    /// </summary>
    /// <param name="fileBytes">Excel 文件字节</param>
    /// <param name="headerRow">表头行（默认 1）</param>
    /// <returns>实体列表</returns>
    List<T> ImportFromExcel<T>(byte[] fileBytes, int headerRow = 1) where T : class, new();

    /// <summary>
    ///     生成导入模板（空 Excel + 表头）
    /// </summary>
    byte[] GenerateImportTemplate<T>(IList<string>? fieldNames = null) where T : class;
}

/// <summary>
///     Excel 服务空实现（默认降级，提示用户注册实现）
///
///     YzhWebBuilder 自动注册此实现，确保 YZH.Core 自带 Controller 不抛异常
///     业务项目层可替换为 EPPlus / NPOI 实现
/// </summary>
public class NullExcelService : IExcelService
{
    public static readonly NullExcelService Instance = new();

    public byte[] ExportToExcel<T>(IList<T> entities, IDictionary<string, string>? columnMapping = null, string sheetName = "Sheet1") where T : class
        => throw new NotImplementedException(
            "[YZH] IExcelService 未注册。YzhWebBuilder 中注册 EPPlus/NPOI 实现：services.AddSingleton<IExcelService, EpplusExcelService>();");

    public List<T> ImportFromExcel<T>(byte[] fileBytes, int headerRow = 1) where T : class, new()
        => throw new NotImplementedException(
            "[YZH] IExcelService 未注册。YzhWebBuilder 中注册 EPPlus/NPOI 实现：services.AddSingleton<IExcelService, EpplusExcelService>();");

    public byte[] GenerateImportTemplate<T>(IList<string>? fieldNames = null) where T : class
        => throw new NotImplementedException(
            "[YZH] IExcelService 未注册。YzhWebBuilder 中注册 EPPlus/NPOI 实现：services.AddSingleton<IExcelService, EpplusExcelService>();");
}
