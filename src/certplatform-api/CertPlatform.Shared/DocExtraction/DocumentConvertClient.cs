using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// DocumentConvertClient 文档转换客户端（共享层，决策 D-5/D-6 落点）
    /// <para>提取链：任意格式 → Markdown（yzh-anydoc 容器；实测 .doc/.xls 直转，无需 doc→docx 中转）</para>
    /// <para>预览链：doc/docx/xls/xlsx/ppt/pptx → PDF（yzh-libreoffice 容器 headless）</para>
    /// <para>中转机制：MinIO 对象 → 挂载目录（宿主 ./anydoc/tmp ↔ 容器 /tmp/anydoc；./libreoffice/tmp ↔ /tmp/libreoffice）→ 转换 → 产物字节</para>
    /// <para>并发说明：由调用方（QueueManager 队列互斥）保证同一时间单任务执行</para>
    /// </summary>
    public class DocumentConvertClient
    {
        private readonly ILogger<DocumentConvertClient> _logger;

        /// <summary>转换工作目录（宿主侧，挂载进容器）</summary>
        private string _workRoot;

        /// <summary>单次转换超时（秒）。PDF 转换实测约 3s，取 120s 兜底大文件</summary>
        public int TimeoutSeconds { get; set; } = 120;

        public DocumentConvertClient(ILogger<DocumentConvertClient> logger)
        {
            _logger = logger;
            // 默认相对仓库 docker/ 目录；生产可通过配置覆盖
            _workRoot = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "docker");
        }

        /// <summary>允许测试/部署环境覆盖工作根目录</summary>
        public void SetWorkRoot(string path) => _workRoot = path;

        // ========================================================
        // 提取链：→ Markdown
        // ========================================================

        /// <summary>
        /// 转换为 Markdown（anydoc 容器）
        /// <para>支持 doc/docx/xls/xlsx/pdf/csv/pptx 等 14 种格式，旧格式直转</para>
        /// </summary>
        public async Task<ConvertResult> ConvertToMarkdownAsync(string fileName, byte[] content)
        {
            return await RunInWorkDirAsync("anydoc", fileName, content, async (containerDir, containerFile, containerOut) =>
            {
                // anydoc 输入文件：/tmp/anydoc/{uuid}/{fileName}
                // 输出：-o /tmp/anydoc/{uuid}/{stem}.md
                // ⚠️ 路径必须加引号：ExecDockerAsync 走容器内 `sh -c`，未引用的空格中文名会被拆成多个参数
                var args = $"anydoc \"{containerFile}\" -o \"{containerOut}\"";
                return await ExecDockerAsync("yzh-anydoc", args, containerOut);
            }, ".md", "anydoc");
        }

        // ========================================================
        // 中间产物链：doc→docx / xls→xlsx（兼容存量队列任务）
        // ========================================================

        /// <summary>
        /// 转换为指定 OpenXML 格式（LibreOffice）
        /// </summary>
        public async Task<ConvertResult> ConvertToFormatAsync(string fileName, byte[] content, string targetFormat)
        {
            return await RunInWorkDirAsync("libreoffice", fileName, content, async (containerDir, containerFile, containerOut) =>
            {
                // ⚠️ 路径必须加引号（同 ConvertToPdfAsync：sh -c 会按空格拆参）
                // ⚠️ UserInstallation 必须放在**会话目录内**：见 ConvertToPdfAsync 的说明
                var args = $"soffice --headless --norestore -env:UserInstallation=file://{containerDir}/.lo-profile " +
                           $"--convert-to {targetFormat} --outdir \"{containerDir}\" \"{containerFile}\"";
                return await ExecDockerAsync("yzh-libreoffice", args, containerOut);
            }, "." + targetFormat.TrimStart('.'), "libreoffice");
        }

        // ========================================================
        // 预览链：→ PDF
        // ========================================================

        /// <summary>
        /// 转换为 PDF（LibreOffice headless）
        /// <para>支持 doc/docx/xls/xlsx/ppt/pptx；独立 UserInstallation profile 避免并发锁</para>
        /// </summary>
        public async Task<ConvertResult> ConvertToPdfAsync(string fileName, byte[] content)
        {
            return await RunInWorkDirAsync("libreoffice", fileName, content, async (containerDir, containerFile, containerOut) =>
            {
                // soffice --headless --norestore 独立 profile --convert-to pdf --outdir /tmp/libreoffice/{uuid}
                // ⚠️ 路径必须加引号：ExecDockerAsync 包装为 `sh -c "..."`，未引用的路径含空格（如「XASL-QM 质量手册.doc」）
                //    会被 shell 拆成两个参数 → soffice 找不到输入文件、退出码 0 且无产物（历史 400「未产出文件」）
                // ★ UserInstallation 必须落在**会话目录内**（`{containerDir}/.lo-profile`）：
                //   原实现写 `/tmp/libreoffice/profile-{GUID}`（= 宿主机挂载目录的顶层）→ 每次转换新建一个 profile 目录，
                //   而 finally 只清 `{sessionId}` 会话目录 → **profile 目录永不回收**。2026-09-26 实测累积 1829 个
                //   `profile-<GUID>` 目录 / 约 568MB。放进会话目录后随会话一起删除。
                var args = $"soffice --headless --norestore -env:UserInstallation=file://{containerDir}/.lo-profile " +
                           $"--convert-to pdf --outdir \"{containerDir}\" \"{containerFile}\"";
                // LibreOffice 输出文件名 = 输入 stem + .pdf（-o 参数不可用，用 outdir 定位）
                return await ExecDockerAsync("yzh-libreoffice", args, containerOut);
            }, ".pdf", "libreoffice");
        }

        // ========================================================
        // 内部：临时目录中转 + docker exec + 产物读取
        // ========================================================

        private async Task<ConvertResult> RunInWorkDirAsync(
            string tool, string fileName, byte[] content,
            Func<string, string, string, Task<ConvertResult>> run,
            string outExt, string toolName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || content == null || content.Length == 0)
                return ConvertResult.Fail("文件名或内容为空", ConvertFailureKind.InvalidInput);

            var sessionId = Guid.NewGuid().ToString("N");
            try
            {
                // 1. 宿主侧创建会话目录（挂载可见）
                var hostDir = Path.Combine(_workRoot, tool, "tmp", sessionId);
                Directory.CreateDirectory(hostDir);

                // 2. 落盘源文件（保留真实扩展名，转换器按扩展名分流）
                var safeName = SanitizeFileName(fileName);
                var hostInput = Path.Combine(hostDir, safeName);
                await File.WriteAllBytesAsync(hostInput, content);

                var stem = Path.GetFileNameWithoutExtension(safeName);
                var containerDir = tool == "anydoc" ? $"/tmp/anydoc/{sessionId}" : $"/tmp/libreoffice/{sessionId}";
                var containerFile = $"{containerDir}/{safeName}";
                var containerOut = $"{containerDir}/{stem}{outExt}";

                // 3. 执行转换
                var result = await run(containerDir, containerFile, containerOut);

                // 4. 读取产物（宿主侧同一路径）
                var hostOut = Path.Combine(hostDir, $"{stem}{outExt}");
                if (result.Success)
                {
                    if (!File.Exists(hostOut))
                    {
                        // LibreOffice 中文文件名时产物名可能与预期不同：扫描目录兜底
                        var produced = Directory.GetFiles(hostDir, $"*{outExt}");
                        if (produced.Length > 0) hostOut = produced[0];
                        else return ConvertResult.Fail($"{toolName} 未产出文件（容器退出码 0 但无 {outExt} 产物）", ConvertFailureKind.NotProduced);
                    }
                    result.Content = await File.ReadAllBytesAsync(hostOut);
                    result.OutputPath = hostOut;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Tool}] 转换异常: {File}", toolName, fileName);
                return ConvertResult.Fail($"{toolName} 转换异常：{ex.Message}", ConvertFailureKind.Other);
            }
            finally
            {
                // 5. 清理会话目录（best effort）
                try
                {
                    var dir = Path.Combine(_workRoot, tool, "tmp", sessionId);
                    if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                }
                catch { /* 清理失败不影响主流程 */ }
            }
        }

        /// <summary>
        /// 执行 docker exec 并等待完成
        /// </summary>
        private async Task<ConvertResult> ExecDockerAsync(string container, string args, string expectedOutput)
        {
            // ⚠️ 必须用 ArgumentList 逐参传递：
            //    Arguments 是单个字符串，.NET 会再做一次引号解析，`sh -c "..."` 中嵌套的引号会被吃掉，
            //    使容器内 sh 把「XASL-QM 质量手册.doc」按空格拆成两个参数（历史 400「未产出文件」根因）。
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add(container);
            psi.ArgumentList.Add("sh");
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(args);

            _logger.LogInformation("[Convert] docker exec {Container}: {Args}", container, args);

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                return ConvertResult.Fail($"转换超时（{TimeoutSeconds}s）：{container}", ConvertFailureKind.Timeout);
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("[Convert] {Container} 退出码 {Code}: {Err}", container, process.ExitCode, stderr);
                var (friendly, kind) = ClassifyError(container, process.ExitCode, stderr, stdout);
                return ConvertResult.Fail(friendly, kind);
            }

            return ConvertResult.Ok();
        }

        /// <summary>
        /// 退出码 → 失败分类（★ 2026-09-26 实测语义，替代原「只做文本匹配」的实现）
        /// <para>anydoc：0=成功｜1=解析失败/不支持的类型｜2=用法错误｜<b>3=需要 OCR</b>（stderr: "page N of M needs OCR"）</para>
        /// <para>soffice：0=成功（也包含「转不动但没报错」，需靠产物缺失判定）｜非 0=执行失败</para>
        /// <para>⚠️ 退出码是**唯一机器可读判据**；文本匹配仅作兜底（改文案不会破坏分派逻辑）</para>
        /// </summary>
        private static (string Message, ConvertFailureKind Kind) ClassifyError(
            string container, int exitCode, string stderr, string stdout)
        {
            var text = $"{stderr}\n{stdout}";

            // ① 退出码优先（机器可读）
            if (container.Contains("anydoc", StringComparison.OrdinalIgnoreCase))
            {
                if (exitCode == 3)
                    return ("该文档为图片/扫描件（无文本层），无法自动提取内容。可手工定义字段与表格，由人工填写",
                            ConvertFailureKind.NeedsOcr);
                if (exitCode == 2)
                    return ($"转换参数错误：{Brief(stderr)}", ConvertFailureKind.InvalidInput);
            }
            if (exitCode == 1 &&
                (text.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("unrecognized", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("not supported", StringComparison.OrdinalIgnoreCase)))
            {
                return ("不支持的文件类型，无法自动提取内容。可手工定义字段与表格，由人工填写",
                        ConvertFailureKind.Unsupported);
            }

            // ② 文本兜底（老版本 anydoc 未返回 3 时）
            if (text.Contains("needs OCR", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("scanned", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("image-based", StringComparison.OrdinalIgnoreCase))
            {
                return ("该文档为图片/扫描件（无文本层），无法自动提取内容。可手工定义字段与表格，由人工填写",
                        ConvertFailureKind.NeedsOcr);
            }
            if (text.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("unrecognized", StringComparison.OrdinalIgnoreCase))
            {
                return ("不支持的文件类型，无法自动提取内容。可手工定义字段与表格，由人工填写",
                        ConvertFailureKind.Unsupported);
            }

            return ($"转换失败：{Brief(stderr)}", ConvertFailureKind.Other);
        }

        /// <summary>截断错误文本，避免超长 stderr 污染 DB 字段（VARCHAR(1024)）</summary>
        private static string Brief(string stderr)
        {
            var t = (stderr ?? "").Trim();
            return t.Length > 300 ? t[..300] : t;
        }

        /// <summary>文件名消毒：去路径分隔符与危险字符</summary>
        private static string SanitizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName) ?? "file";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "file" : name;
        }
    }

    /// <summary>
    /// 转换失败分类（★ 机器可读，2026-09-26）
    /// <para>动机：原实现只把退出码翻译成中文文本，调用方**无法区分「需要 OCR」与「文件损坏」**，
    /// 只能去匹配中文提示 → 一改文案分派逻辑就断。本枚举把退出码语义固化下来。</para>
    /// </summary>
    public enum ConvertFailureKind
    {
        /// <summary>未失败</summary>
        None = 0,
        /// <summary>需要 OCR（anydoc 退出码 3）：图片 / 扫描件（无文本层）</summary>
        NeedsOcr = 1,
        /// <summary>不支持的文件类型</summary>
        Unsupported = 2,
        /// <summary>转换超时</summary>
        Timeout = 3,
        /// <summary>容器退出码 0 但未产出文件</summary>
        NotProduced = 4,
        /// <summary>入参非法（文件名/内容为空、参数错误）</summary>
        InvalidInput = 5,
        /// <summary>其它失败</summary>
        Other = 99
    }

    /// <summary>转换结果</summary>
    public class ConvertResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public byte[]? Content { get; set; }
        /// <summary>产物宿主路径（调试用）</summary>
        public string? OutputPath { get; set; }

        /// <summary>失败分类（Success=true 时恒为 None）</summary>
        public ConvertFailureKind FailureKind { get; set; } = ConvertFailureKind.None;

        /// <summary>是否需要 OCR 链路（图片 / 扫描件）</summary>
        public bool NeedsOcr => FailureKind == ConvertFailureKind.NeedsOcr;

        public static ConvertResult Ok() => new() { Success = true };

        public static ConvertResult Fail(string message, ConvertFailureKind kind = ConvertFailureKind.Other)
            => new() { Success = false, Message = message, FailureKind = kind };
    }
}
