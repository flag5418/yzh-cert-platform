using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 编码/路径生成服务
/// 职责：生成目录编码(SDC-)、文件夹编码(FD-)、文件编码(FL-)、MinIO存储路径(V3)
/// </summary>
public class CodeGeneratorService
{
    #region 编码生成

    /// <summary>
    /// 生成标准目录编码：SDC-{StandardCode}|{PhaseCode}
    /// </summary>
    public string GenerateDirectoryCode(string standardCode, string phaseCode)
    {
        var cleanStandard = CleanCode(standardCode);
        var cleanPhase = CleanCode(phaseCode);
        return $"SDC-{cleanStandard}|{cleanPhase}";
    }

    /// <summary>
    /// 生成文件夹编码：FD-{DirectoryCode}|L{Level}|S{Sequence}
    /// </summary>
    public string GenerateFolderCode(string directoryCode, int level, int sequence)
    {
        return $"FD-{directoryCode}|L{level:D2}|S{sequence:D3}";
    }

    /// <summary>
    /// 生成文件编码：FL-{FolderCode}|{FileName}
    /// </summary>
    public string GenerateFileCode(string folderCode, string fileName)
    {
        return $"FL-{folderCode}|{fileName}";
    }

    #endregion

    #region V3 MinIO 路径生成（标准目录）

    /// <summary>
    /// 生成标准目录存储路径
    /// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
    /// 编码段做 CleanCode，文件夹路径与文件名保持原样（仅去路径分隔符）
    /// </summary>
    public string GenerateStandardDirectoryPath(string orgCode, string standardCode,
        string phaseCode, string folderPath, string fileName)
    {
        var cleanOrg = CleanCode(orgCode);
        var cleanStandard = CleanCode(standardCode);
        var cleanPhase = CleanCode(phaseCode);
        var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";

        var segments = new List<string> { cleanOrg, cleanStandard, cleanPhase };
        if (!string.IsNullOrEmpty(cleanFolderPath))
            segments.Add(cleanFolderPath);
        segments.Add(SanitizeFileName(fileName));

        var path = string.Join("/", segments.Where(s => !string.IsNullOrEmpty(s)));
        return $"/standard-directory/{path}";
    }

    /// <summary>
    /// 生成转换后文件存储路径
    /// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/.converted/{FileName}
    /// </summary>
    public string GenerateConvertedStoragePath(string orgCode, string standardCode,
        string phaseCode, string folderPath, string fileName)
    {
        var cleanOrg = CleanCode(orgCode);
        var cleanStandard = CleanCode(standardCode);
        var cleanPhase = CleanCode(phaseCode);
        var cleanFolderPath = folderPath?.Replace("|", "-").Replace("//", "/").Trim('/') ?? "";

        var segments = new List<string> { cleanOrg, cleanStandard, cleanPhase };
        if (!string.IsNullOrEmpty(cleanFolderPath))
            segments.Add(cleanFolderPath);
        segments.Add(".converted");
        segments.Add(SanitizeFileName(fileName));

        var path = string.Join("/", segments.Where(s => !string.IsNullOrEmpty(s)));
        return $"/standard-directory/{path}";
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 清理编码中的特殊字符（用于路径段）
    /// </summary>
    public string CleanCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        return code.Replace(":", "").Replace("-", "").Replace(" ", "")
                   .Replace("/", "").Replace("\\", "");
    }

    /// <summary>
    /// 文件名安全化：仅去除路径分隔符，保留空格/连字符/中文等原样
    /// </summary>
    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "";
        return fileName.Replace("/", "").Replace("\\", "");
    }

    #endregion
}
