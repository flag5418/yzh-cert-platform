using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CertPlatform.Shared.DocExtraction;

/// <summary>为文档提取的实际输入生成稳定指纹。</summary>
public static class DocumentExtractionFingerprint
{
    /// <summary>保留字段边界后计算 SHA-256。</summary>
    public static string Compute(params string[] values)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
