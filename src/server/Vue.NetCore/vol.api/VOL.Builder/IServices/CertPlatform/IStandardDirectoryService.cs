/*
 * 标准目录管理 Service 接口
 * 
 * 职责：
 *   1. 标准目录配置的 CRUD 操作
 *   2. 标准目录文件夹的 CRUD 操作
 *   3. 标准目录文件的 CRUD 操作
 *   4. 编码生成服务
 *   5. 导出打包功能
 */
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Dir;
using VOL.Entity.DomainModels;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IStandardDirectoryService : IDependency
    {
        #region 组织树

        /// <summary>
        /// 获取组织树数据
        /// 格式：机构 -> 标准 -> 阶段
        /// </summary>
        WebResponseContent GetOrganizationTree();

        #endregion

        #region 标准目录配置

        /// <summary>
        /// 获取标准目录配置列表
        /// </summary>
        PageGridData<StandardDirectoryConfig> GetConfigs(PageDataOptions options);

        /// <summary>
        /// 获取单个标准目录配置
        /// </summary>
        StandardDirectoryConfig GetConfig(string directoryCode);

        /// <summary>
        /// 创建标准目录配置
        /// </summary>
        WebResponseContent CreateConfig(StandardDirectoryConfig config);

        /// <summary>
        /// 更新标准目录配置
        /// </summary>
        WebResponseContent UpdateConfig(StandardDirectoryConfig config);

        /// <summary>
        /// 删除标准目录配置
        /// </summary>
        WebResponseContent DeleteConfig(string directoryCode);

        #endregion

        #region 标准目录文件夹

        /// <summary>
        /// 获取阶段的完整文件树（含规则属性）
        /// 用于文档提取规则管理页面，单次返回所有层级
        /// </summary>
        /// <param name="directoryCode">目录编码</param>
        /// <returns>完整的文件夹+文件树JSON</returns>
        StageFileTreeResponse GetStageFileTree(string directoryCode);

        /// <summary>
        /// 获取标准目录文件夹树
        /// </summary>
        WebResponseContent GetFolderTree(string directoryCode);

        /// <summary>
        /// 创建标准目录文件夹
        /// </summary>
        WebResponseContent CreateFolder(StandardDirectoryFolder folder);

        /// <summary>
        /// 更新标准目录文件夹
        /// </summary>
        WebResponseContent UpdateFolder(StandardDirectoryFolder folder);

        /// <summary>
        /// 删除标准目录文件夹
        /// </summary>
        WebResponseContent DeleteFolder(string folderCode);

        #endregion

        #region 标准目录文件

        /// <summary>
        /// 获取标准目录文件列表
        /// </summary>
        WebResponseContent GetFiles(string folderCode);
        WebResponseContent GetFilesByDirectory(string directoryCode);

        /// <summary>
        /// 创建标准目录文件
        /// </summary>
        WebResponseContent CreateFile(StandardDirectoryFile file);

        /// <summary>
        /// 更新标准目录文件
        /// </summary>
        WebResponseContent UpdateFile(StandardDirectoryFile file);

        /// <summary>
        /// 删除标准目录文件
        /// </summary>
        WebResponseContent DeleteFile(string fileCode);

        #endregion

        #region 导出打包

        /// <summary>
        /// 将标准目录配置及其子文件夹、文件打包成ZIP
        /// </summary>
        /// <param name="directoryCode">目录编码</param>
        /// <param name="folderCodes">选中的文件夹编码列表</param>
        /// <param name="fileCodes">选中的文件编码列表</param>
        /// <returns>ZIP文件流</returns>
        Task<Stream> ExportAsZip(string directoryCode, List<string> folderCodes, List<string> fileCodes);

        /// <summary>
        /// 从 MinIO 下载单个文件
        /// </summary>
        /// <param name="storagePath">MinIO 存储路径</param>
        /// <returns>(流, contentType, 文件名)</returns>
        Task<(Stream stream, string contentType, string fileName)> DownloadFile(string storagePath);

        #endregion

        #region 文件上传

        /// <summary>
        /// 上传文件到标准目录（旧版，保留兼容）
        /// </summary>
        /// <param name="file">上传的文件</param>
        /// <param name="directoryCode">目录编码</param>
        /// <param name="relativePath">相对路径（支持文件夹结构）</param>
        /// <returns>操作结果</returns>
        Task<WebResponseContent> UploadFile(IFormFile file, string directoryCode, string relativePath);

        /// <summary>
        /// 批量上传预初始化
        /// 接收客户端清单，预创建编码和数据库记录，返回增强清单
        /// </summary>
        /// <param name="manifest">客户端基础清单</param>
        /// <returns>增强清单</returns>
        Task<WebResponseContent> UploadInit(UploadManifestRequest manifest);

        /// <summary>
        /// 上传单个文件到MinIO（新版，基于taskId）
        /// </summary>
        /// <param name="file">上传的文件</param>
        /// <param name="request">上传请求参数</param>
        /// <returns>操作结果</returns>
        Task<WebResponseContent> UploadFileWithTask(IFormFile file, UploadFileRequest request);

        /// <summary>
        /// 确认上传完成
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>操作结果</returns>
        Task<WebResponseContent> UploadConfirm(string taskId);

        /// <summary>
        /// 回滚上传任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>操作结果</returns>
        Task<WebResponseContent> UploadCancel(string taskId);

        /// <summary>
        /// 查询上传任务状态
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>任务状态</returns>
        UploadStatusResponse GetUploadStatus(string taskId);

        /// <summary>
        /// 重试失败的文档转换（failed 或孤儿 pending 的 doc/xls 重新入队）
        /// </summary>
        /// <returns>操作结果（含入队数量）</returns>
        Task<WebResponseContent> RetryFailedConversionsAsync();

        /// <summary>
        /// 查询某目录（机构/标准/阶段）下运行中的队列
        /// 返回 null 表示无运行中队列
        /// </summary>
        Task<object> GetActiveQueueAsync(string directoryCode);

        /// <summary>
        /// 批量查询文件编码在运行中队列中的锁定状态，返回 { fileCode: queueCode }
        /// </summary>
        Task<Dictionary<string, string>> GetFileLockStatusAsync(List<string> fileCodes);

        #endregion
    }
}
