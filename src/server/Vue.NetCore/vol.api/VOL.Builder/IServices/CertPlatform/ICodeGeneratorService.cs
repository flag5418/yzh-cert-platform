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
    }
}
