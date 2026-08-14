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

        /// <summary>
        /// 生成 MinIO 存储路径 V2（四级结构）
        /// 格式：/{OrgCode}/{CleanStandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// 示例：/CB001/ISO134852016/STAGE01/质量手册/程序文件.docx
        /// </summary>
        public string GenerateStoragePathV2(string orgCode, string standardCode, string phaseCode,
                                            string folderPath, string fileName)
        {
            // 清理编码中的特殊字符
            var cleanStandard = CleanCode(standardCode);
            var cleanPhase = CleanCode(phaseCode);
            var cleanOrg = CleanCode(orgCode);
            
            // 清理文件夹路径中的特殊字符
            var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";
            
            // 生成完整路径
            if (string.IsNullOrEmpty(cleanFolderPath))
            {
                return $"/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";
            }
            
            return $"/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
        }

        /// <summary>
        /// 生成转换后文件的存储路径
        /// 格式：/{OrgCode}/{CleanStandardCode}/{PhaseCode}/{FolderPath}/.converted/{FileName}
        /// </summary>
        public string GenerateConvertedStoragePath(string orgCode, string standardCode, string phaseCode,
                                                   string folderPath, string fileName)
        {
            // 清理编码中的特殊字符
            var cleanStandard = CleanCode(standardCode);
            var cleanPhase = CleanCode(phaseCode);
            var cleanOrg = CleanCode(orgCode);
            
            // 清理文件夹路径中的特殊字符
            var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";
            
            // 生成完整路径（转换后文件放在 .converted 隐藏目录下）
            if (string.IsNullOrEmpty(cleanFolderPath))
            {
                return $"/{cleanOrg}/{cleanStandard}/{cleanPhase}/.converted/{fileName}";
            }
            
            return $"/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/.converted/{fileName}";
        }

        #endregion

        #region V3 OSS 路径生成（双顶层文件夹）

        /// <summary>
        /// 生成标准目录存储路径
        /// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        public string GenerateStandardDirectoryPath(string orgCode, string standardCode,
                                                    string phaseCode, string folderPath, string fileName)
        {
            var cleanOrg = CleanCode(orgCode);
            var cleanStandard = CleanCode(standardCode);
            var cleanPhase = CleanCode(phaseCode);
            var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";

            if (string.IsNullOrEmpty(cleanFolderPath))
                return $"/standard-directory/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";

            return $"/standard-directory/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
        }

        /// <summary>
        /// 生成企业资料存储路径
        /// 格式：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        public string GenerateEnterpriseDocumentPath(string enterpriseNo, string orgCode,
            string standardCode, string phaseCode, string folderPath, string fileName)
        {
            var cleanEnt = CleanCode(enterpriseNo);
            var cleanOrg = CleanCode(orgCode);
            var cleanStandard = CleanCode(standardCode);
            var cleanPhase = CleanCode(phaseCode);
            var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";

            if (string.IsNullOrEmpty(cleanFolderPath))
                return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";

            return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
        }

        /// <summary>
        /// 生成转换后文件存储路径
        /// 格式：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/.converted/{FileName}
        /// </summary>
        public string GenerateEnterpriseConvertedPath(string enterpriseNo, string orgCode,
            string standardCode, string phaseCode, string folderPath, string fileName)
        {
            var cleanEnt = CleanCode(enterpriseNo);
            var cleanOrg = CleanCode(orgCode);
            var cleanStandard = CleanCode(standardCode);
            var cleanPhase = CleanCode(phaseCode);
            var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";

            if (string.IsNullOrEmpty(cleanFolderPath))
                return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/.converted/{fileName}";

            return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/.converted/{fileName}";
        }

        #endregion

        #region V2 路径生成（废弃，保留兼容）

        /// <summary>
        /// 清理编码中的特殊字符（用于路径）
        /// </summary>
        private string CleanCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "";

            return code.Replace(":", "")
                       .Replace("-", "")
                       .Replace(" ", "")
                       .Replace("/", "")
                       .Replace("\\", "");
        }

        #endregion
    }
}
