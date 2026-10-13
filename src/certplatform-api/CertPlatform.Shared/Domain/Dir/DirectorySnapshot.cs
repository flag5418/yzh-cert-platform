using System;
using System.Collections.Generic;
using System.Linq;
using CertPlatform.Shared.Domain.Organization;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Shared.Domain.Dir
{
    /// <summary>
    /// 目录域<b>只读切片快照</b>：一个目录配置（或一个文件夹）的
    /// 「有效文件夹 + 有效文件」在<b>保留段过滤</b>与<b>统一排序口径</b>之后的不可变视图。
    ///
    /// <para><b>★ 为什么存在</b>（2026-10-13 · 核心资源检索统一）：原先 4 个读端点
    /// （folders-flat / files / root-files / stage-files）各自取数、各自过滤、各自排序，
    /// 排序口径分叉到 3 种（索引序 / Sort 单键 / 无 ORDER BY）⇒ 左树与右列表顺序不一致
    /// 且无任何报错。本类把「过滤 + 排序 + 切片」收口成<b>一处</b>，端点只做 DTO 投影。</para>
    ///
    /// <para><b>★ 纯函数纪律</b>：本类无状态、无 IO（构造后不可变）；
    /// 取数一律在 <see cref="DirectoryReadModel"/>（经 <c>ICoreResourceQuery</c>）。</para>
    ///
    /// <para><b>★ 排序口径</b>（2026-10-13 用户裁决 ①）：
    /// 文件夹 <c>Sort → FolderName（Ordinal）</c>；文件 <c>Sort → FileName（Ordinal）</c>
    /// —— 兜底键必须确定性且人类可读，⛔ 不用 Code（GUID ⇒ 退化为随机序）。</para>
    /// </summary>
    public sealed class DirectorySnapshot
    {
        /// <summary>
        /// 空快照（ConfigCode = ""，Folders/Files/RootFiles 全空）——
        /// 入参不合法（空 / 脏值）时的<b>防御性返回</b>，⛔ 不发任何查询。
        /// </summary>
        public static readonly DirectorySnapshot Empty = Create("", Array.Empty<StandardDirectoryFolder>(), Array.Empty<StandardDirectoryFile>());

        private readonly HashSet<string> _folderCodeSet;
        private readonly Dictionary<string, List<StandardDirectoryFile>> _filesByFolder;

        /// <summary>目录配置 Code（<c>cert_standard_directory_config.Code</c>）</summary>
        public string ConfigCode { get; }

        /// <summary>
        /// 有效文件夹（已剔除保留段及其子孙），按 <c>Sort → FolderName</c> 排序。
        /// </summary>
        public IReadOnlyList<StandardDirectoryFolder> Folders { get; }

        /// <summary>
        /// 有效文件（已剔除挂在保留段文件夹下的文件），按 <c>Sort → FileName</c> 排序。
        /// </summary>
        public IReadOnlyList<StandardDirectoryFile> Files { get; }

        /// <summary>
        /// <b>根级 / 孤儿文件</b> = <c>FolderCode ∉ 有效文件夹集</c>（2026-10-13 用户裁决 ②）。
        /// <para>三种情形都归根级：① 真根级（<c>FolderCode == ""</c>，决策 ⑨）；
        /// ② 悬空（文件夹被软删 / 禁用，文件仍在）；③ 文件夹不存在（脏数据）。
        /// ⛔ 保留段文件夹下的文件<b>不在此列</b>（构造时已随保留段一并剔除）。</para>
        /// <para>⚠️ 与旧根级端点的差异：旧视图判据是 <c>FolderCode == ""</c> 严格相等，
        /// 悬空文件<b>不可见</b>；新判据把它们显示为「根目录」—— 这是裁决 ② 的有意变更。</para>
        /// </summary>
        public IReadOnlyList<StandardDirectoryFile> RootFiles { get; }

        private DirectorySnapshot(
            string configCode,
            List<StandardDirectoryFolder> folders,
            List<StandardDirectoryFile> files)
        {
            ConfigCode = configCode;
            Folders = folders;
            Files = files;

            _folderCodeSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in folders)
            {
                var code = f.Code ?? "";
                if (code.Length > 0) _folderCodeSet.Add(code);
            }

            _filesByFolder = new Dictionary<string, List<StandardDirectoryFile>>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var fc = file.FolderCode ?? "";
                if (!_filesByFolder.TryGetValue(fc, out var list))
                {
                    list = new List<StandardDirectoryFile>();
                    _filesByFolder[fc] = list;
                }
                list.Add(file);
            }

            RootFiles = files
                .Where(f => !_folderCodeSet.Contains(f.FolderCode ?? ""))
                .ToList();
        }

        /// <summary>
        /// 由<b>原始</b>（未过滤、未排序）的文件夹 / 文件列表构建快照。
        ///
        /// <para>处理顺序（⛔ 不可调换）：</para>
        /// <list type="number">
        ///   <item>收集保留段文件夹及其子孙 Code；</item>
        ///   <item>剔除保留段文件夹 <b>与挂在保留段文件夹下的文件</b>；</item>
        ///   <item>过滤<b>之后</b>才建立有效文件夹 Code 集 —— 否则保留段文件夹的文件
        ///   会因「父不在集合内」被误判成根级孤儿（假阳性）；</item>
        ///   <item>按统一口径排序（文件夹 Sort→FolderName，文件 Sort→FileName）；
        ///   稳定排序保证同键项保持输入序（DB 索引序），跨端点一致的前提。</item>
        /// </list>
        /// </summary>
        public static DirectorySnapshot Create(
            string? configCode,
            IEnumerable<StandardDirectoryFolder> folders,
            IEnumerable<StandardDirectoryFile> files)
        {
            var folderList = folders.ToList();
            var fileList = files.ToList();

            var reserved = DirectoryVisibility.CollectReservedFolderCodes(folderList);
            if (reserved.Count > 0)
            {
                folderList = folderList
                    .Where(f => !reserved.Contains(f.Code ?? ""))
                    .ToList();
                fileList = fileList
                    .Where(f => !reserved.Contains(f.FolderCode ?? ""))
                    .ToList();
            }

            var orderedFolders = OrgStandardTreeAssembler.OrderFolders(
                folderList, f => f.Sort, f => f.FolderName);
            var orderedFiles = OrgStandardTreeAssembler.OrderFiles(
                fileList, f => f.Sort, f => f.FileName);

            return new DirectorySnapshot(configCode ?? "", orderedFolders, orderedFiles);
        }

        /// <summary>
        /// 某文件夹下的文件（输入已按 <c>Sort → FileName</c> 排序，过滤保持相对顺序）。
        /// <para>⛔ 未知文件夹 Code（含空串）返回空列表 —— 根级文件走 <see cref="RootFiles"/>。</para>
        /// <para>★ 不变量（2026-10-13 单测钉住）：每个文件<b>恰属</b>一处 ——
        /// 父文件夹有效 ⇒ <c>FilesOf(父)</c>；否则 ⇒ <see cref="RootFiles"/>。
        /// 故此处必须同时校验 folderCode ∈ 有效文件夹集，否则悬空文件会
        /// 「既在 RootFiles、又能被 FilesOf 捞到」（两处同时出现 = 分区被破坏）。</para>
        /// </summary>
        public IReadOnlyList<StandardDirectoryFile> FilesOf(string? folderCode)
        {
            if (string.IsNullOrEmpty(folderCode)) return Array.Empty<StandardDirectoryFile>();
            if (!_folderCodeSet.Contains(folderCode)) return Array.Empty<StandardDirectoryFile>();
            return _filesByFolder.TryGetValue(folderCode, out var list)
                ? list
                : Array.Empty<StandardDirectoryFile>();
        }
    }
}
