/*
 * 编码生成服务实现
 * 
 * 编码规则（简化版）：
 *   标准目录编码：SDC-{StandardCode}|{PhaseCode}
 *   文件夹编码：FD-{DirectoryCode}|L{Level}|S{Sequence}
 *   文件编码：FL-{FolderCode}|{FileName}
 * 
 * MinIO 存储路径：
 *   /{StandardCode}/{PhaseCode}/{FolderCode}/{FileName}
 */
using System;
using System.Linq;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.Services.CertPlatform
{
    public class CodeGeneratorService : ICodeGeneratorService, IDependency
    {
        #region 编码生成

        /// <summary>
        /// 生成标准目录编码：SDC-{StandardCode}|{PhaseCode}
        /// 示例：SDC-ISO9001|PH01
        /// </summary>
        public string GenerateDirectoryCode(string standardCode, string phaseCode)
        {
            // 清理编码中的特殊字符
            var cleanStandard = standardCode.Replace(":", "").Replace("-", "").Replace(" ", "");
            var cleanPhase = phaseCode.Replace(":", "").Replace("-", "").Replace(" ", "");
            
            return $"SDC-{cleanStandard}|{cleanPhase}";
        }

        /// <summary>
        /// 生成文件夹编码：FD-{DirectoryCode}|L{Level}|S{Sequence}
        /// 示例：FD-SDC-ISO9001|PH01|L01|S001
        /// </summary>
        public string GenerateFolderCode(string directoryCode, int level, int sequence)
        {
            return $"FD-{directoryCode}|L{level:D2}|S{sequence:D3}";
        }

        /// <summary>
        /// 生成文件编码：FL-{FolderCode}|{FileName}
        /// 示例：FL-FD-SDC-ISO9001|PH01|L01|S001|营业执照正本.pdf
        /// </summary>
        public string GenerateFileCode(string folderCode, string fileName)
        {
            return $"FL-{folderCode}|{fileName}";
        }

        #endregion

        #region MinIO 路径生成

        /// <summary>
        /// 生成 MinIO 存储路径（简化版）
        /// 格式：/{StandardCode}/{PhaseCode}/{FolderCode}/{FileName}
        /// 示例：/ISO9001/PH01/FD-SDC-ISO9001|PH01|L01|S001/营业执照正本.pdf
        /// </summary>
        public string GenerateStoragePath(string standardCode, string phaseCode, 
                                           string folderCode, string fileName)
        {
            // 清理编码中的特殊字符，用于路径
            var cleanStandard = standardCode.Replace(":", "").Replace("-", "").Replace(" ", "");
            var cleanPhase = phaseCode.Replace(":", "").Replace("-", "").Replace(" ", "");
            var cleanFolder = folderCode.Replace("|", "-");
            
            // 生成完整路径
            return $"/{cleanStandard}/{cleanPhase}/{cleanFolder}/{fileName}";
        }

        /// <summary>
        /// 解析 MinIO 存储路径（简化版）
        /// </summary>
        public (string StandardCode, string PhaseCode, string FolderCode, string FileName) 
                ParseStoragePath(string path)
        {
            var parts = path.TrimStart('/').Split('/');
            
            // parts[0] = StandardCode
            // parts[1] = PhaseCode
            // parts[2] = FolderCode (| 已替换为 -)
            // parts[3] = FileName
            
            if (parts.Length < 4)
                throw new ArgumentException("Invalid storage path format");
            
            var folderCode = parts[2].Replace("-", "|");
            var fileName = parts[3];
            
            return (parts[0], parts[1], folderCode, fileName);
        }

        #endregion
    }
}
