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
                var args = $"soffice --headless --norestore -env:UserInstallation=file:///tmp/libreoffice/profile-{Guid.NewGuid():N} " +
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
                var args = $"soffice --headless --norestore -env:UserInstallation=file:///tmp/libreoffice/profile-{Guid.NewGuid():N} " +
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
                return ConvertResult.Fail("文件名或内容为空");

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
                        else return ConvertResult.Fail($"{toolName} 未产出文件（容器退出码 0 但无 {outExt} 产物）");
                    }
                    result.Content = await File.ReadAllBytesAsync(hostOut);
                    result.OutputPath = hostOut;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Tool}] 转换异常: {File}", toolName, fileName);
                return ConvertResult.Fail($"{toolName} 转换异常：{ex.Message}");
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
                return ConvertResult.Fail($"转换超时（{TimeoutSeconds}s）：{container}");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("[Convert] {Container} 退出码 {Code}: {Err}", container, process.ExitCode, stderr);
                var friendly = ClassifyError(stderr, stdout);
                return ConvertResult.Fail(friendly);
            }

            return ConvertResult.Ok();
        }

        /// <summary>错误分类（对照《文档预览链路改造方案-V1》§4.3 退出码语义）</summary>
        private static string ClassifyError(string stderr, string stdout)
        {
            var text = $"{stderr}\n{stdout}";
            if (text.Contains("OCR", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("scanned", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("image-based", StringComparison.OrdinalIgnoreCase))
                return "该文档为扫描件（无文本层），需要 OCR 链路（暂未接入），请使用文字版文件";
            if (text.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
                return "不支持的文件类型";
            var brief = stderr.Length > 300 ? stderr[..300] : stderr;
            return $"转换失败：{brief}";
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

    /// <summary>转换结果</summary>
    public class ConvertResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public byte[]? Content { get; set; }
        /// <summary>产物宿主路径（调试用）</summary>
        public string? OutputPath { get; set; }

        public static ConvertResult Ok() => new() { Success = true };
        public static ConvertResult Fail(string message) => new() { Success = false, Message = message };
    }
}
