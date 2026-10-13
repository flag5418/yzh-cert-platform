using System;
using System.Collections.Generic;
using System.Linq;
using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Storage;

namespace CertPlatform.Shared.Domain.Dir
{
    /// <summary>
    /// 目录域「产物 / 系统保留段」可见性过滤 —— <b>唯一实现</b>。
    ///
    /// <para><b>★ 迁入说明（2026-10-13 · 核心资源检索统一）</b>：原为
    /// <c>CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs</c> 的私有方法
    /// （<c>CollectReservedFolderCodes</c> / <c>IsReservedSegmentName</c>）。Admin 与将来的 Auditor
    /// 读侧必须用<b>同一套</b>保留段口径 —— 两端各写一份 ⇒ 「左树隐藏、右列表能列出」的不一致必然复发。
    /// Admin 侧私有副本已删除，调用点改走本类。</para>
    ///
    /// <para><b>为什么要过滤</b>：<c>pdf</c> / <c>markdown</c> / <c>editable</c> /
    /// <c>_template</c> / <c>_archive</c> 是 <see cref="PathBuilder.ReservedSegments"/> 的保留段名 ——
    /// 系统把转换产物写在与业务文件夹物理同层的目录下。正常路径下
    /// <c>ValidateFolderOrFileName</c> 已挡住新建，但历史数据 / 直改 DB / 模板导入仍可能留下
    /// 同名文件夹 ⇒ 显示层统一过滤，否则管理员会在目录树里看到自己没建过的「pdf」文件夹。</para>
    /// </summary>
    public static class DirectoryVisibility
    {
        /// <summary>树遍历节点上限：脏数据防御性兜底（与 Admin 原实现一致）</summary>
        private const int MaxTreeNodes = 5000;

        /// <summary>
        /// 收集「产物 / 系统目录」及其全部子孙的 Code（大小写敏感集合，保留段名比对不敏感）。
        /// <para>用于显示层过滤保留段名文件夹 <b>及其下所有文件</b>。</para>
        /// <para>★ 带环检测与深度上限 —— 脏数据 ParentCode 成环时不能无限递归。</para>
        /// </summary>
        public static HashSet<string> CollectReservedFolderCodes(List<StandardDirectoryFolder> folders)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var roots = folders.Where(f => IsReservedSegmentName(f.FolderName)).ToList();
            if (roots.Count == 0) return result;

            // 子孙展开（迭代而非递归：脏数据成环时不会栈溢出）
            var frontier = roots.ToList();
            while (frontier.Count > 0)
            {
                var next = new List<StandardDirectoryFolder>();
                foreach (var node in frontier)
                {
                    var code = node.Code ?? "";
                    if (string.IsNullOrEmpty(code) || !result.Add(code)) continue;
                    foreach (var child in folders.Where(f => f.ParentCode == code))
                    {
                        if (!result.Contains(child.Code ?? "")) next.Add(child);
                    }
                }
                if (result.Count > MaxTreeNodes) break;
                frontier = next;
            }
            return result;
        }

        /// <summary>文件夹名是否为产物 / 系统保留段名（pdf / markdown / editable / _template / _archive）</summary>
        public static bool IsReservedSegmentName(string? name)
            => !string.IsNullOrWhiteSpace(name)
               && PathBuilder.ReservedSegments.Any(s =>
                   string.Equals(s, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
