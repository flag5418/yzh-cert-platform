using System.Threading.Tasks;
using CertPlatform.Shared.Domain.Scope;

namespace CertPlatform.Shared.Domain.Dir
{
    /// <summary>
    /// 目录域<b>唯一读模型</b>：取数（经 <c>ICoreResourceQuery</c>）+ 构建
    /// <see cref="DirectorySnapshot"/>。
    ///
    /// <para><b>★ 铁律</b>：端内（Admin / Auditor）读目录一律走本类，
    /// ⛔ 不准再自写 <c>GetListAsync&lt;StandardDirectoryFolder/File&gt;</c> 与过滤 / 排序
    /// —— 「同一个 WHERE 出现在第二处 ⇒ 就是违规」（守卫 A-R6 的精神在读模型层的延伸）。</para>
    ///
    /// <para><b>★ 两个加载入口</b>（覆盖 4+1 个读端点）：</para>
    /// <list type="bullet">
    ///   <item><see cref="LoadAsync"/>：已知配置 Code ⇒ folders-flat / stage-files /
    ///   root-files / folder-tree；</item>
    ///   <item><see cref="LoadByFolderCodeAsync"/>：只知文件夹 Code ⇒ files 端点
    ///   （先反查文件夹拿 ConfigCode，⛔ 否则无法限定配置范围 —— P0-3 全库泄漏的根源）。</item>
    /// </list>
    ///
    /// <para><b>★ 生命周期</b>：Scoped（持 <c>ICoreResourceQuery</c>）。
    /// 快照不跨请求缓存（目录编辑频繁，缓存失效协议比收益贵）。</para>
    /// </summary>
    public sealed class DirectoryReadModel
    {
        private readonly ICoreResourceQuery _core;

        public DirectoryReadModel(ICoreResourceQuery core)
        {
            _core = core;
        }

        /// <summary>
        /// 按<b>目录配置 Code</b> 加载快照（2 次查询：文件夹 + 文件）。
        /// <para>⛔ 空 / 空白 <paramref name="configCode"/> ⇒ 返回空快照，<b>不发任何查询</b>
        /// —— 防御「空串走全表」的 P0-3 同类漏洞。</para>
        /// </summary>
        public async Task<DirectorySnapshot> LoadAsync(string? configCode)
        {
            if (string.IsNullOrWhiteSpace(configCode))
                return DirectorySnapshot.Empty;

            var folders = await _core.GetDirectoryFoldersAsync(new[] { configCode });
            var files = await _core.GetDirectoryFilesByConfigCodeAsync(configCode);
            return DirectorySnapshot.Create(configCode, folders, files);
        }

        /// <summary>
        /// 按<b>文件夹 Code</b> 加载其所属配置的快照（3 次查询：文件夹单查 + 配置文件夹 + 配置文件）。
        ///
        /// <para>★ <paramref name="folderCode"/> 找不到 / 无 ConfigCode ⇒ 返回<b>空快照</b>。
        /// 旧实现（<c>GetDirectoryFilesByFolderCodeAsync</c>）只按 FolderCode 匹配、
        /// <b>不限配置</b> —— 文件夹 Code 是 GUID 通常安全，但一旦为空 / 脏值就是全库扫描
        /// （P0-3）。新实现以文件夹行的 ConfigCode 定界，语义同时更正确：</para>
        /// <list type="bullet">
        ///   <item>文件夹行<b>含已禁用</b>（<c>includeDisabled: true</c>）—— 与旧口径一致：
        ///   禁用文件夹下的有效文件仍要能列出（否则「停用再启用」期间文件消失）；</item>
        ///   <item>文件夹<b>已软删 / 不存在</b> ⇒ 空快照（旧实现会按残留 FolderCode 匹配出
        ///   幽灵文件；裁决 ② 下这些文件应出现在<b>原配置的根级</b>，由 RootFiles 兜住）。</item>
        /// </list>
        /// </summary>
        public async Task<DirectorySnapshot> LoadByFolderCodeAsync(string? folderCode)
        {
            if (string.IsNullOrWhiteSpace(folderCode))
                return DirectorySnapshot.Empty;

            var folder = await _core.GetDirectoryFolderByCodeAsync(folderCode, includeDisabled: true);
            if (folder == null || string.IsNullOrWhiteSpace(folder.ConfigCode))
                return DirectorySnapshot.Empty;

            return await LoadAsync(folder.ConfigCode);
        }
    }
}
