using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOL.Core.Configuration
{
    /// <summary>
    /// 国密 SM2/SM3/SM4 配置（对应 appsettings.json 中 GmCrypto 节点）
    /// </summary>
    public class GmCryptoOptions
    {
        /// <summary>
        /// SM2 密钥（十六进制字符串，与库 GenerateSecretKeyPair(true) 格式一致）
        /// </summary>
        public GmSm2Options SM2 { get; set; } = new GmSm2Options();

        /// <summary>
        /// SM3 为杂凑算法，标准实现无密钥；此处仅提供开关等可选项
        /// </summary>
        public GmSm3Options SM3 { get; set; } = new GmSm3Options();

        /// <summary>
        /// SM4 对称密钥与 IV（十六进制，密钥 32 个 hex 字符即 16 字节；CBC 时 IV 同长度）
        /// </summary>
        public GmSm4Options SM4 { get; set; } = new GmSm4Options();
    }

    public class GmSm3Options
    {
        /// <summary>是否允许通过 GmCryptoHelper 使用 SM3（默认 true）</summary>
        public bool Enabled { get; set; } = true;
    }

    public class GmSm2Options
    {
        /// <summary>公钥 Hex（可选，加密/验签用）</summary>
        public string PublicKeyHex { get; set; }

        /// <summary>私钥 Hex（可选，解密/签名用）</summary>
        public string PrivateKeyHex { get; set; }

        /// <summary>C1C3C2 顺序（与 Arric 库 Encrypt 第三个参数一致）</summary>
        public bool PreferC1C3C2 { get; set; } = true;
    }

    public class GmSm4Options
    {
        /// <summary>16 字节密钥 Hex（32 字符）</summary>
        public string KeyHex { get; set; }

        /// <summary>CBC 模式 IV Hex（16 字节）；ECB 可不填</summary>
        public string IvHex { get; set; }

        /// <summary>CBC 或 ECB，默认 CBC</summary>
        public string Mode { get; set; } = "CBC";
    }
}
