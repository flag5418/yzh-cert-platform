using System.Collections.Generic;

namespace VOL.Entity.CertPlatform.Dir
{
    /// <summary>
    /// 上传清单请求DTO（客户端基础清单）
    /// 客户端选择文件后生成此JSON，发送给服务端进行预处理
    /// </summary>
    public class UploadManifestRequest
    {
        /// <summary>
        /// 目标目录编码
        /// </summary>
        public string DirectoryCode { get; set; }

        /// <summary>
        /// 机构编码（前端从组织树节点点击得到 cbCode；
        /// 架构约定：维护/管理端机构与登录人无关，来自节点关系；审核员登录后由后端从登录信息解析）
        /// </summary>
        public string OrgCode { get; set; }

        /// <summary>
        /// 所属标准编码（如 ISO9001）
        /// </summary>
        public string StandardCode { get; set; }

        /// <summary>
        /// 所属阶段编码（如 PH01）
        /// </summary>
        public string PhaseCode { get; set; }

        /// <summary>
        /// 需要创建的文件夹列表（前端从文件路径中提取去重）
        /// </summary>
        public List<FolderItem> Folders { get; set; } = new List<FolderItem>();

        /// <summary>
        /// 待上传文件列表
        /// </summary>
        public List<FileItem> Files { get; set; } = new List<FileItem>();
    }

    /// <summary>
    /// 文件夹项
    /// </summary>
    public class FolderItem
    {
        /// <summary>
        /// 文件夹路径（如：4记录文件/内审记录）
        /// </summary>
        public string Path { get; set; }
    }

    /// <summary>
    /// 文件项
    /// </summary>
    public class FileItem
    {
        /// <summary>
        /// 文件的相对路径（含文件夹层级）
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// MIME类型
        /// </summary>
        public string MimeType { get; set; }
    }

    /// <summary>
    /// 上传清单响应DTO（服务端增强清单）
    /// 服务端处理后返回，补充了编码、OSS路径等信息
    /// </summary>
    public class UploadManifestResponse
    {
        /// <summary>
        /// 任务状态
        /// </summary>
        public string Status { get; set; } = "initialized";

        /// <summary>
        /// 任务唯一ID
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// 目标目录编码
        /// </summary>
        public string DirectoryCode { get; set; }

        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// 总文件大小（字节）
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// 文件夹列表（含编码）
        /// </summary>
        public List<EnhancedFolderItem> Folders { get; set; } = new List<EnhancedFolderItem>();

        /// <summary>
        /// 文件列表（含编码和OSS路径）
        /// </summary>
        public List<EnhancedFileItem> Files { get; set; } = new List<EnhancedFileItem>();
    }

    /// <summary>
    /// 增强的文件夹项
    /// </summary>
    public class EnhancedFolderItem
    {
        /// <summary>
        /// 文件夹编码
        /// </summary>
        public string FolderCode { get; set; }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string FolderName { get; set; }

        /// <summary>
        /// 父文件夹编码
        /// </summary>
        public string ParentCode { get; set; }

        /// <summary>
        /// 深度
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// 完整路径
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// 操作模式：create=新增, reuse=复用已有文件夹
        /// </summary>
        public string Mode { get; set; } = "create";
    }

    /// <summary>
    /// 增强的文件项
    /// </summary>
    public class EnhancedFileItem
    {
        /// <summary>
        /// 文件在清单中的序号
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 文件编码（服务端生成）
        /// </summary>
        public string FileCode { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件相对路径
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// 完整路径（从根到文件）
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// MIME类型
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// MinIO存储路径
        /// </summary>
        public string StoragePath { get; set; }

        /// <summary>
        /// 所属文件夹编码
        /// </summary>
        public string ParentFolderCode { get; set; }

        /// <summary>
        /// 操作模式：create=新增, replace=替换已有文件
        /// </summary>
        public string Mode { get; set; } = "create";

        /// <summary>
        /// 替换模式下，已有文件的编码（用于定位要更新的记录）
        /// </summary>
        public string ExistingFileCode { get; set; }

        /// <summary>
        /// 替换模式下，已有文件的主键ID（用于直接更新记录）
        /// </summary>
        public long? ExistingFileId { get; set; }

        /// <summary>
        /// 替换模式下，旧文件的MinIO路径（用于删除旧对象）
        /// </summary>
        public string OldStoragePath { get; set; }

        /// <summary>
        /// 状态（pending/uploading/active/failed）
        /// </summary>
        public string Status { get; set; } = "pending";
    }

    /// <summary>
    /// 上传文件请求DTO（单个文件上传时使用）
    /// </summary>
    public class UploadFileRequest
    {
        /// <summary>
        /// 增强清单中的 fileCode
        /// </summary>
        public string FileCode { get; set; }

        /// <summary>
        /// 增强清单中的 storagePath
        /// </summary>
        public string StoragePath { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        public string TaskId { get; set; }
    }

    /// <summary>
    /// 任务状态查询响应DTO
    /// </summary>
    public class UploadStatusResponse
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// 已成功上传数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败数
        /// </summary>
        public int FailCount { get; set; }

        /// <summary>
        /// 文件列表（含状态）
        /// </summary>
        public List<FileStatusItem> Files { get; set; } = new List<FileStatusItem>();
    }

    /// <summary>
    /// 文件状态项
    /// </summary>
    public class FileStatusItem
    {
        public string FileCode { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// 上传文件V2请求DTO（解决 Swagger [FromForm] + IFormFile 问题）
    /// </summary>
    public class UploadFileV2Dto
    {
        /// <summary>
        /// 文件内容
        /// </summary>
        public Microsoft.AspNetCore.Http.IFormFile File { get; set; }

        /// <summary>
        /// 文件编码
        /// </summary>
        public string FileCode { get; set; }

        /// <summary>
        /// MinIO存储路径
        /// </summary>
        public string StoragePath { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        public string TaskId { get; set; }
    }

    /// <summary>
    /// 导出打包请求DTO
    /// 用户勾选需要导出的文件夹和文件后提交
    /// </summary>
    public class ExportRequest
    {
        /// <summary>
        /// 选中的文件夹编码列表
        /// </summary>
        public List<string> FolderCodes { get; set; } = new List<string>();

        /// <summary>
        /// 选中的文件编码列表
        /// </summary>
        public List<string> FileCodes { get; set; } = new List<string>();
    }

    #region 阶段文件树响应（文档提取规则管理用）

    /// <summary>
    /// 阶段完整文件树响应
    /// 用于文档提取规则管理页面，单次返回所有层级的文件夹和文件
    /// </summary>
    public class StageFileTreeResponse
    {
        /// <summary>
        /// 目录编码
        /// </summary>
        public string DirectoryCode { get; set; }

        /// <summary>
        /// 文件夹树（含子文件夹和文件）
        /// </summary>
        public List<StageFolderNode> Folders { get; set; } = new List<StageFolderNode>();

        /// <summary>
        /// 统计信息
        /// </summary>
        public StageFileStatistics Statistics { get; set; } = new StageFileStatistics();
    }

    /// <summary>
    /// 阶段文件夹节点
    /// </summary>
    public class StageFolderNode
    {
        /// <summary>
        /// 文件夹编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 父级编码
        /// </summary>
        public string ParentCode { get; set; }

        /// <summary>
        /// 深度层级
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// 子文件夹列表
        /// </summary>
        public List<StageFolderNode> Children { get; set; } = new List<StageFolderNode>();

        /// <summary>
        /// 该文件夹下的文件列表
        /// </summary>
        public List<StageFileNode> Files { get; set; } = new List<StageFileNode>();
    }

    /// <summary>
    /// 阶段文件节点
    /// </summary>
    public class StageFileNode
    {
        /// <summary>
        /// 文件编码
        /// </summary>
        public string FileCode { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 所属文件夹编码
        /// </summary>
        public string FolderCode { get; set; }

        /// <summary>
        /// MinIO存储路径
        /// </summary>
        public string StoragePath { get; set; }

        /// <summary>
        /// 转换后文件的 MinIO 存储路径（.doc→.docx, .xls→.xlsx）
        /// </summary>
        public string ConvertedStoragePath { get; set; }

        /// <summary>
        /// 转换状态：null/pending/converting/completed/failed
        /// </summary>
        public string ConvertStatus { get; set; }

        /// <summary>
        /// 转换失败原因
        /// </summary>
        public string ConvertMessage { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// MIME类型
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// 规则状态：none=未配置, configured=已配置, failed=失败
        /// </summary>
        public string RuleStatus { get; set; } = "none";

        /// <summary>
        /// 已提取的字段数
        /// </summary>
        public int ExtractFieldCount { get; set; }

        /// <summary>
        /// 已定义的表格数
        /// </summary>
        public int TableDefCount { get; set; }
    }

    /// <summary>
    /// 阶段文件统计信息
    /// </summary>
    public class StageFileStatistics
    {
        /// <summary>
        /// 总文件夹数
        /// </summary>
        public int TotalFolders { get; set; }

        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// 已配置规则的文件数
        /// </summary>
        public int ConfiguredFiles { get; set; }
    }

    #endregion
}
