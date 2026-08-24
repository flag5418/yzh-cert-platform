/*
 * 编码生成服务接口
 * 
 * 职责：
 *   1. 生成标准目录编码
 *   2. 生成文件夹编码
 *   3. 生成文件编码
 *   4. 生成MinIO存储路径
 */
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface ICodeGeneratorService : IDependency
    {
        /// <summary>
        /// 生成标准目录编码：SDC-{StandardCode}|{PhaseCode}
        /// </summary>
        string GenerateDirectoryCode(string standardCode, string phaseCode);

        /// <summary>
        /// 生成文件夹编码：FD-{DirectoryCode}|L{Level}|S{Sequence}
        /// </summary>
        string GenerateFolderCode(string directoryCode, int level, int sequence);

        /// <summary>
        /// 生成文件编码：FL-{FolderCode}|{FileName}
        /// </summary>
        string GenerateFileCode(string folderCode, string fileName);

        /// <summary>
        /// 生成 MinIO 存储路径
        /// 格式：/{StandardCode}/{PhaseCode}/{FolderCode}/{FileName}
        /// </summary>
        string GenerateStoragePath(string standardCode, string phaseCode, 
                                   string folderCode, string fileName);

        /// <summary>
        /// 解析 MinIO 存储路径
        /// </summary>
        (string StandardCode, string PhaseCode, string FolderCode, string FileName) 
         ParseStoragePath(string path);

        /// <summary>
        /// 生成 MinIO 存储路径 V2（四级结构）
        /// 格式：/{OrgCode}/{CleanStandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// 示例：/CB001/ISO134852016/STAGE01/质量手册/程序文件.docx
        /// </summary>
        /// <param name="orgCode">企业编码</param>
        /// <param name="standardCode">标准编码（会自动清理特殊字符）</param>
        /// <param name="phaseCode">阶段编码</param>
        /// <param name="folderPath">文件夹路径（相对于阶段根目录）</param>
        /// <param name="fileName">文件名</param>
        string GenerateStoragePathV2(string orgCode, string standardCode, string phaseCode,
                                     string folderPath, string fileName);

        /// <summary>
        /// 生成转换后文件的存储路径
        /// 格式：/{OrgCode}/{CleanStandardCode}/{PhaseCode}/{FolderPath}/.converted/{FileName}
        /// </summary>
        string GenerateConvertedStoragePath(string orgCode, string standardCode, string phaseCode,
                                            string folderPath, string fileName);

        // ====== V3 OSS 路径生成（双顶层文件夹） ======

        /// <summary>
        /// 生成标准目录存储路径
        /// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        string GenerateStandardDirectoryPath(string orgCode, string standardCode,
                                             string phaseCode, string folderPath, string fileName);

        /// <summary>
        /// 生成企业资料存储路径
        /// 格式：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        string GenerateEnterpriseDocumentPath(string enterpriseNo, string orgCode,
            string standardCode, string phaseCode, string folderPath, string fileName);

        /// <summary>
        /// 生成转换后文件存储路径
        /// 格式：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/.converted/{FileName}
        /// </summary>
        string GenerateEnterpriseConvertedPath(string enterpriseNo, string orgCode,
            string standardCode, string phaseCode, string folderPath, string fileName);
    }
}
