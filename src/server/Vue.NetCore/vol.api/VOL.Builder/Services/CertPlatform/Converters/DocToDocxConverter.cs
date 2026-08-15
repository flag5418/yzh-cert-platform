/*
 * DOC 转 DOCX 转换器
 * 使用 Docker 中的 LibreOffice 命令行工具实现
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace VOL.Builder.Services.CertPlatform.Converters
{
    /// <summary>
    /// DOC 转 DOCX 转换器
    /// 使用 Docker 中的 LibreOffice 命令行工具
    /// </summary>
    public class DocToDocxConverter
    {
        private readonly string _tempDirectory;
        private readonly string _dockerContainerName;
        
        public DocToDocxConverter()
        {
            // Docker 容器名称
            _dockerContainerName = "yzh-libreoffice";
            
            // 临时工作目录（与 Docker 共享）
            // 注意：必须与 docker-compose.yml 中的挂载路径一致
            // compose.yml: ./libreoffice/tmp:/tmp/libreoffice
            _tempDirectory = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docker/libreoffice/tmp";
            
            // 确保临时目录存在
            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }
        
        /// <summary>
        /// 将 DOC 文件转换为 DOCX 格式
        /// </summary>
        /// <param name="inputStream">输入 DOC 文件流</param>
        /// <param name="outputStream">输出 DOCX 文件流</param>
        /// <returns>转换结果</returns>
        public async Task<ConvertResult> ConvertAsync(Stream inputStream, Stream outputStream)
        {
            var guid = Guid.NewGuid().ToString("N");
            var tempInputPath = Path.Combine(_tempDirectory, $"input_{guid}.doc");
            var tempOutputDir = Path.Combine(_tempDirectory, $"output_{guid}");
            
            // Docker 容器内的路径
            var dockerTempDir = "/tmp/libreoffice";
            var dockerInputPath = $"{dockerTempDir}/input_{guid}.doc";
            var dockerOutputDir = $"{dockerTempDir}/output_{guid}";
            
            try
            {
                // 保存输入流到临时文件
                using (var fileStream = new FileStream(tempInputPath, FileMode.Create, FileAccess.Write))
                {
                    await inputStream.CopyToAsync(fileStream);
                }
                
                // 创建输出目录
                Directory.CreateDirectory(tempOutputDir);
                
                // 检查 LibreOffice 容器是否运行
                if (!IsContainerRunning())
                {
                    return new ConvertResult
                    {
                        Success = false,
                        Message = "LibreOffice Docker 容器未运行"
                    };
                }
                
                // 构建 Docker 执行命令
                // 关键：-env:UserInstallation 为每次转换指定独立 profile 目录，避免并发时多个
                // LibreOffice 实例争抢共享 profile（/root/.config/libreoffice/4）锁导致静默失败
                // （退出码 1、stderr 为空、无输出文件），实测 5 并发下旧命令 2/5 失败、新命令 5/5 成功
                var dockerProfileDir = $"{dockerTempDir}/profile_{guid}";
                var arguments = $"exec {_dockerContainerName} libreoffice --headless " +
                                $"-env:UserInstallation=file://{dockerProfileDir} " +
                                $"--convert-to docx --outdir {dockerOutputDir} {dockerInputPath}";
                
                // 执行转换
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) outputBuilder.AppendLine(e.Data);
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null) errorBuilder.AppendLine(e.Data);
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // 等待转换完成（超时 60 秒）
                var completed = process.WaitForExit(60000);
                
                if (!completed)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex) { Console.WriteLine($"[DocToDocxConverter] Error: {ex.Message}"); }
                    
                    return new ConvertResult
                    {
                        Success = false,
                        Message = "转换超时（超过 60 秒）"
                    };
                }
                
                Console.WriteLine($"[DocToDocxConverter] LibreOffice exit code: {process.ExitCode}");
                Console.WriteLine($"[DocToDocxConverter] Output: {outputBuilder}");
                Console.WriteLine($"[DocToDocxConverter] Error: {errorBuilder}");
                
                if (process.ExitCode != 0)
                {
                    return new ConvertResult
                    {
                        Success = false,
                        Message = $"LibreOffice 转换失败，退出码: {process.ExitCode}, 错误: {errorBuilder}"
                    };
                }
                
                // 查找转换后的文件（在宿主机目录中）
                var outputFiles = Directory.GetFiles(tempOutputDir, "*.docx");
                if (outputFiles.Length == 0)
                {
                    return new ConvertResult
                    {
                        Success = false,
                        Message = "未找到转换后的文件"
                    };
                }
                
                // 读取转换后的文件到输出流
                using (var fileStream = new FileStream(outputFiles[0], FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(outputStream);
                }
                
                return new ConvertResult
                {
                    Success = true,
                    Message = "转换成功"
                };
            }
            catch (Exception ex)
            {
                return new ConvertResult
                {
                    Success = false,
                    Message = $"转换失败: {ex.Message}"
                };
            }
            finally
            {
                // 清理临时文件（含独立 profile 目录，避免每次转换在 /tmp/libreoffice 累积）
                try
                {
                    if (File.Exists(tempInputPath))
                    {
                        File.Delete(tempInputPath);
                    }
                    
                    if (Directory.Exists(tempOutputDir))
                    {
                        Directory.Delete(tempOutputDir, true);
                    }
                    
                    var tempProfileDir = Path.Combine(_tempDirectory, $"profile_{guid}");
                    if (Directory.Exists(tempProfileDir))
                    {
                        Directory.Delete(tempProfileDir, true);
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[DocToDocxConverter] Error: {ex.Message}"); }
            }
        }
        
        /// <summary>
        /// 检查 LibreOffice Docker 容器是否运行
        /// </summary>
        public bool IsAvailable()
        {
            return IsContainerRunning();
        }
        
        /// <summary>
        /// 检查 Docker 容器是否运行
        /// </summary>
        private bool IsContainerRunning()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = $"inspect -f '{{{{.State.Running}}}}' {_dockerContainerName}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = process.StandardOutput.ReadToEnd().Trim();
                var error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit(5000);
                
                Console.WriteLine($"[DocToDocxConverter] Docker inspect exit code: {process.ExitCode}, output: {output}, error: {error}");
                
                // 输出可能包含单引号，需要去除
                var cleanOutput = output.ToLower().Trim('\'');
                return process.ExitCode == 0 && cleanOutput == "true";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocToDocxConverter] IsContainerRunning exception: {ex.Message}");
                return false;
            }
        }
    }
}
