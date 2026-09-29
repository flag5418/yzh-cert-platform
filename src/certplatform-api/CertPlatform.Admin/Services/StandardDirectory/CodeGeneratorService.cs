using CertPlatform.Shared.Storage;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 产物路径派生（委托给 <see cref="PathBuilder"/> —— MinIO 路径唯一权威）。
///
/// <para>⛔ <b>2026-09-26 已删除复合编码生成</b>（《存储与编码规范 V4》§2.1）：</para>
/// <list type="bullet">
///   <item><c>GenerateDirectoryCode</c>（SDC-…）、<c>GenerateFolderCode</c>（FD-…）、
///         <c>GenerateFileCode</c>（FL-…）—— 复合码内嵌文件名 / 序号，既是判重陷阱
///         （改名 = 换码 = 永不匹配），也让路径段与 DB 值不可逆。</item>
///   <item><c>GenerateStandardDirectoryPath</c> —— 路径一律由 <see cref="PathBuilder.StandardFile"/> 构造。</item>
///   <item><c>CleanCode</c> —— 会删掉 GUID 里的 <c>-</c>，使路径段与数据库 Code 不一致。</item>
/// </list>
///
/// <para>实体的业务键即 <c>BaseEntity.Code</c>（GUID），由框架自动生成。</para>
/// </summary>
public static class CodeGeneratorService
{
    /// <summary>产物类型段名（MinIO 里就是普通 key 前缀，不是真目录）</summary>
    public const string ProductKindPdf = PathBuilder.PdfSegment;
    public const string ProductKindMarkdown = PathBuilder.MarkdownSegment;

    /// <summary>
    /// 从**源文件存储路径**派生产物路径（预览 PDF / 提取 Markdown）。
    /// <para>★ 实现已收口到 <see cref="PathBuilder.Product"/> —— 本方法仅为兼容既有调用点而保留，
    /// <b>不要再在这里加逻辑</b>。语义与返回值契约见 <see cref="PathBuilder.Product"/>。</para>
    /// </summary>
    public static string BuildProductPath(string storagePath, string productKind, string targetExt)
        => PathBuilder.Product(storagePath, productKind, targetExt);

    /// <summary>
    /// 判断给定路径是否为「产物路径」（位于 <c>pdf/</c> 或 <c>markdown/</c> 段下）。
    /// <para>★ 实现已收口到 <see cref="PathBuilder.IsProductPath"/>。</para>
    /// </summary>
    public static bool IsProductPath(string? path)
        => PathBuilder.IsProductPath(path);
}
