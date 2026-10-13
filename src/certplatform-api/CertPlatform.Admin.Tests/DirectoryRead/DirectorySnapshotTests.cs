using System.Collections.Generic;
using System.Linq;
using CertPlatform.Shared.Domain.Dir;
using CertPlatform.Shared.Entities.Dir;
using Xunit;

namespace CertPlatform.Admin.Tests.DirectoryRead;

/// <summary>
/// ★ <b>目录域唯一读切片</b>（<see cref="DirectorySnapshot"/> + <see cref="DirectoryVisibility"/>）的单测。
///
/// <para><b>为什么要测</b>（2026-10-13 · 1.B.6d 核心资源检索统一）：这两个类是
/// folders / folders-flat / files / root-files / stage-files 五个端点的<b>共同口径</b>，
/// 一旦被改坏 ⇒ 多个页面同时错且<b>不报错</b>（集合差 / 顺序差都只表现为「看起来不对」）。
/// 纯函数 ⇒ 单测是最便宜的防复发件。</para>
///
/// <para>⛔ 全程不碰 DB / 不碰 Moq（本仓测试约定：纯逻辑直测）。</para>
/// </summary>
public class DirectorySnapshotTests
{
    // ───────────────────────── 构造助手（只填切片要用到的键） ─────────────────────────

    private static StandardDirectoryFolder F(string code, string name,
        string parent = "", int sort = 0, string? cfg = "cfg1")
        => new() { Code = code, FolderName = name, ParentCode = parent, Sort = sort, ConfigCode = cfg };

    private static StandardDirectoryFile Fi(string code, string name,
        string folder = "", int sort = 0, string? cfg = "cfg1")
        => new() { Code = code, FileName = name, FolderCode = folder, Sort = sort, ConfigCode = cfg ?? "" };

    // ───────────────────────── ① 保留段过滤（顺序依赖 —— 本次修复的核心） ─────────────────────────

    /// <summary>
    /// ★ 过滤顺序不可调换的回归测试：保留段文件夹<b>与其下文件</b>必须在建
    /// folderCodeSet <b>之前</b>剔除 —— 否则保留段下的文件会因「父不在集合内」
    /// 被误判成<b>根级孤儿</b>（假阳性 ⇒ 根目录突然冒出 pdf 转换产物）。
    /// </summary>
    [Fact]
    public void Create_保留段文件夹与其下文件_一并剔除且不产生假根级孤儿()
    {
        var folders = new List<StandardDirectoryFolder>
        {
            F("f1", "制度文件"),
            F("pdf", "pdf"),                 // 产物目录（保留段）
            F("pdf-child", "x", parent: "pdf") // 保留段的子目录，同样要被剔除
        };
        var files = new List<StandardDirectoryFile>
        {
            Fi("a", "a.doc", folder: "f1"),
            Fi("p1", "p.pdf", folder: "pdf"),            // 挂在保留段下
            Fi("p2", "p2.pdf", folder: "pdf-child"),     // 挂在保留段的子目录下
            Fi("r1", "root.md", folder: "")              // 真·根级
        };

        var snap = DirectorySnapshot.Create("cfg1", folders, files);

        // 保留段文件夹 + 子孙全被剔除
        Assert.DoesNotContain(snap.Folders, f => f.Code == "pdf" || f.Code == "pdf-child");
        Assert.Single(snap.Folders, f => f.Code == "f1");

        // 保留段下文件被剔除，且 ⛔ 没有变成根级孤儿
        Assert.DoesNotContain(snap.Files, f => f.Code == "p1" || f.Code == "p2");
        Assert.DoesNotContain(snap.RootFiles, f => f.Code == "p1" || f.Code == "p2");

        // 真·根级文件仍在
        Assert.Single(snap.RootFiles, f => f.Code == "r1");
    }

    /// <summary>保留段名判定：大小写不敏感 + 首尾空白容忍。</summary>
    [Theory]
    [InlineData("pdf")]
    [InlineData("PDF")]
    [InlineData("  Markdown  ")]
    [InlineData("_Template")]
    public void CollectReserved_保留段名_大小写与空白不敏感(string name)
    {
        var codes = DirectoryVisibility.CollectReservedFolderCodes(
            new List<StandardDirectoryFolder> { F("x", name) });
        Assert.Contains("x", codes);
    }

    /// <summary>非保留段名（哪怕含 pdf 子串）不误伤 —— 判定是整名相等，不是 contains。</summary>
    [Fact]
    public void CollectReserved_普通文件夹名不被误伤()
    {
        var codes = DirectoryVisibility.CollectReservedFolderCodes(
            new List<StandardDirectoryFolder> { F("a", "制度文件"), F("b", "pdf规范") });
        Assert.Empty(codes);
    }

    /// <summary>子孙展开：保留段根文件夹的<b>整棵子树</b>都要被收进来（迭代展开，不递归）。</summary>
    [Fact]
    public void CollectReserved_子孙文件夹一并收集()
    {
        var folders = new List<StandardDirectoryFolder>
        {
            F("pdf", "pdf"),
            F("c1", "子1", parent: "pdf"),
            F("c2", "子2", parent: "c1"),
            F("keep", "制度文件")
        };
        var codes = DirectoryVisibility.CollectReservedFolderCodes(folders);
        Assert.Equal(3, codes.Count);
        Assert.Contains("pdf", codes);
        Assert.Contains("c1", codes);
        Assert.Contains("c2", codes);
        Assert.DoesNotContain("keep", codes);
    }

    // ───────────────────────── ② 根级判据（裁决 ②：FolderCode ∉ 有效文件夹集） ─────────────────────────

    /// <summary>
    /// 悬空 <c>FolderCode</c>（指向的文件夹已软删/不存在）⇒ 归根级，不再凭空消失。
    /// 这是裁决 ② 相对旧实现（严格 <c>FolderCode == ""</c>）的<b>有意变更</b>。
    /// </summary>
    [Fact]
    public void RootFiles_悬空FolderCode_归根级()
    {
        var folders = new List<StandardDirectoryFolder> { F("f1", "制度文件") };
        var files = new List<StandardDirectoryFile>
        {
            Fi("ok", "ok.doc", folder: "f1"),
            Fi("ghost", "ghost.doc", folder: "deleted-folder"), // 指向已不存在的文件夹
            Fi("root", "root.doc", folder: "")
        };

        var snap = DirectorySnapshot.Create("cfg1", folders, files);

        Assert.Equal(2, snap.RootFiles.Count);
        Assert.Contains(snap.RootFiles, f => f.Code == "root");
        Assert.Contains(snap.RootFiles, f => f.Code == "ghost"); // 悬空 ⇒ 根级可见
        Assert.DoesNotContain(snap.RootFiles, f => f.Code == "ok");
    }

    // ───────────────────────── ③ 排序口径（裁决 ①：Sort → 确定性兜底键） ─────────────────────────

    /// <summary>文件夹：Sort → FolderName（Ordinal）。Sort 相同才落到 FolderName。</summary>
    [Fact]
    public void Create_文件夹排序_Sort优先_FolderName兜底()
    {
        var folders = new List<StandardDirectoryFolder>
        {
            F("1", "乙", sort: 0),
            F("2", "甲", sort: 0),
            F("3", "丙", sort: -1)   // Sort 小的在最前，无视名字
        };

        var snap = DirectorySnapshot.Create("cfg1", folders, new List<StandardDirectoryFile>());

        // 期望序 = Sort 升序，Sort 相同按 FolderName Ordinal：乙(U+4E59) < 甲(U+7532)
        Assert.Equal(new[] { "3", "1", "2" }, snap.Folders.Select(f => f.Code).ToArray());
    }

    /// <summary>文件：Sort → FileName（Ordinal）。与文件夹同一口径（OrderFiles 委托 OrderFolders）。</summary>
    [Fact]
    public void Create_文件排序_Sort优先_FileName兜底()
    {
        var files = new List<StandardDirectoryFile>
        {
            Fi("1", "b.doc", sort: 0),
            Fi("2", "a.doc", sort: 0),
            Fi("3", "c.doc", sort: -1)
        };

        var snap = DirectorySnapshot.Create("cfg1", new List<StandardDirectoryFolder>(), files);

        Assert.Equal(new[] { "3", "2", "1" }, snap.Files.Select(f => f.Code).ToArray());
    }

    /// <summary>
    /// 稳定排序：同 Sort 同名的文件保持<b>输入序</b>（DB 索引序）——
    /// 「跨端点一致」依赖这一点（各端点输入序相同 ⇒ 输出序相同）。
    /// </summary>
    [Fact]
    public void Create_完全同键_保持输入序()
    {
        var files = new List<StandardDirectoryFile>
        {
            Fi("first", "x.doc"), Fi("second", "x.doc"), Fi("third", "x.doc")
        };

        var snap = DirectorySnapshot.Create("cfg1", new List<StandardDirectoryFolder>(), files);

        Assert.Equal(new[] { "first", "second", "third" },
            snap.Files.Select(f => f.Code).ToArray());
    }

    // ───────────────────────── ④ FilesOf（切片取数） ─────────────────────────

    /// <summary>已知文件夹 ⇒ 按快照序返回其下文件；空/未知 ⇒ 空列表（⛔ 不返回全表）。</summary>
    [Fact]
    public void FilesOf_已知返回有序文件_空与未知返回空()
    {
        var folders = new List<StandardDirectoryFolder> { F("f1", "制度文件") };
        var files = new List<StandardDirectoryFile>
        {
            Fi("2", "b.doc", folder: "f1", sort: 0),
            Fi("1", "a.doc", folder: "f1", sort: 0),
            Fi("o", "other.doc", folder: "f2")
        };

        var snap = DirectorySnapshot.Create("cfg1", folders, files);

        Assert.Equal(new[] { "1", "2" }, snap.FilesOf("f1").Select(f => f.Code).ToArray());
        Assert.Empty(snap.FilesOf("f2"));   // f2 不在有效文件夹集 ⇒ 空（其文件归根级）
        Assert.Empty(snap.FilesOf(""));     // 空 Code ⇒ 空（P0-3：绝不返回全表）
        Assert.Empty(snap.FilesOf(null));
    }

    // ───────────────────────── ⑤ Empty / configCode 归一 ─────────────────────────

    /// <summary>configCode null/空 ⇒ 归一为 ""（LoadAsync 对白名单入参的约定），且不抛。</summary>
    [Fact]
    public void Create_configCode为空_归一为空串且不抛()
    {
        var snap = DirectorySnapshot.Create(null, new List<StandardDirectoryFolder>(),
            new List<StandardDirectoryFile>());
        Assert.Equal("", snap.ConfigCode);
        Assert.Empty(snap.Folders);
        Assert.Empty(snap.Files);
        Assert.Empty(snap.RootFiles);
    }

    /// <summary>Empty 单例：各属性恒为空 —— LoadAsync 的短路返回值。</summary>
    [Fact]
    public void Empty_各属性恒空()
    {
        Assert.Equal("", DirectorySnapshot.Empty.ConfigCode);
        Assert.Empty(DirectorySnapshot.Empty.Folders);
        Assert.Empty(DirectorySnapshot.Empty.Files);
        Assert.Empty(DirectorySnapshot.Empty.RootFiles);
        Assert.Empty(DirectorySnapshot.Empty.FilesOf("anything"));
    }
}
