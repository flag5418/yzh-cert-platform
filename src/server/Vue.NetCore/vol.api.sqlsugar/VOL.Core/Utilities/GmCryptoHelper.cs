using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arric.Crypto.SM.SM2;
using Arric.Crypto.SM.SM3;
using Arric.Crypto.SM.SM4;
using VOL.Core.Configuration;

namespace VOL.Core.Utilities
{
    /// <summary>
    /// 国密 SM2 / SM3 / SM4 工具（密钥取自 AppSetting.GmCrypto）
    /// </summary>
    public static class GmCryptoHelper
    {
        /// <summary>
        /// SM3 摘要（Hex，由 Arric.Crypto.SM 返回）
        /// </summary>
        public static string Sm3HashHex(string plainText, GmCryptoOptions opt = null)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM3 != null && !opt.SM3.Enabled)
                throw new InvalidOperationException("GmCrypto:SM3:Enabled 为 false，已禁用 SM3");
            var sm3 = new SM3Crypto();
            return sm3.EncryptToHex(plainText);
        }

        /// <summary>
        /// 使用配置中公钥加密为 Hex（若未配置公钥则抛异常）
        /// </summary>
        public static string Sm2EncryptToHex(string plainText, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM2 == null || string.IsNullOrWhiteSpace(opt.SM2.PublicKeyHex))
                throw new InvalidOperationException("GmCrypto:SM2:PublicKeyHex 未配置");
            var sm2 = new SM2Crypto();
            return sm2.EncryptToHex(plainText, opt.SM2.PublicKeyHex.Trim(), opt.SM2.PreferC1C3C2);
        }

        /// <summary>
        /// 使用配置中私钥解密 Hex 密文
        /// </summary>
        public static string Sm2DecryptFromHex(string cipherHex, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM2 == null || string.IsNullOrWhiteSpace(opt.SM2.PrivateKeyHex))
                throw new InvalidOperationException("GmCrypto:SM2:PrivateKeyHex 未配置");
            var sm2 = new SM2Crypto();
            return sm2.DecryptFormHex(cipherHex, opt.SM2.PrivateKeyHex.Trim(), opt.SM2.PreferC1C3C2);
        }

        /// <summary>
        /// SM2 签名（Hex）
        /// </summary>
        public static string Sm2SignToHex(string plainText, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM2 == null || string.IsNullOrWhiteSpace(opt.SM2.PrivateKeyHex))
                throw new InvalidOperationException("GmCrypto:SM2:PrivateKeyHex 未配置");
            return SM2Signature.GenerateSignatureToHex(plainText, opt.SM2.PrivateKeyHex.Trim(), opt.SM2.PreferC1C3C2);
        }

        /// <summary>
        /// SM2 验签
        /// </summary>
        public static bool Sm2VerifySignature(string plainText, string signatureHex, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM2 == null || string.IsNullOrWhiteSpace(opt.SM2.PublicKeyHex))
                throw new InvalidOperationException("GmCrypto:SM2:PublicKeyHex 未配置");
            return SM2Signature.VerifySignature(plainText, signatureHex, opt.SM2.PublicKeyHex.Trim(), opt.SM2.PreferC1C3C2, true);
        }

        /// <summary>
        /// SM4 CBC 加密为 Hex（使用配置中的 KeyHex / IvHex）
        /// </summary>
        public static string Sm4EncryptCbcToHex(string plainText, Encoding encoding = null, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            ValidateSm4(opt);
            encoding ??= Encoding.UTF8;
            var crypto = new Sm4Crypto { Encoding = encoding };
            return crypto.EncryptCBCToHex(plainText, opt.SM4.KeyHex.Trim(), opt.SM4.IvHex.Trim());
        }

        /// <summary>
        /// SM4 CBC 解密 Hex
        /// </summary>
        public static string Sm4DecryptCbcFromHex(string cipherHex, Encoding encoding = null, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            ValidateSm4(opt);
            encoding ??= Encoding.UTF8;
            var crypto = new Sm4Crypto { Encoding = encoding };
            return crypto.DecryptCBCFormHex(cipherHex, opt.SM4.KeyHex.Trim(), opt.SM4.IvHex.Trim());
        }

        /// <summary>
        /// SM4 ECB 加密为 Hex（无需 IV）
        /// </summary>
        public static string Sm4EncryptEcbToHex(string plainText, Encoding encoding = null, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM4 == null || string.IsNullOrWhiteSpace(opt.SM4.KeyHex))
                throw new InvalidOperationException("GmCrypto:SM4:KeyHex 未配置");
            encoding ??= Encoding.UTF8;
            var crypto = new Sm4Crypto { Encoding = encoding };
            return crypto.EncryptECBToHex(plainText, opt.SM4.KeyHex.Trim());
        }

        /// <summary>
        /// SM4 ECB 解密 Hex
        /// </summary>
        public static string Sm4DecryptEcbFromHex(string cipherHex, Encoding encoding = null, GmCryptoOptions opt = null)
        {
            opt ??= AppSetting.GmCrypto;
            if (opt?.SM4 == null || string.IsNullOrWhiteSpace(opt.SM4.KeyHex))
                throw new InvalidOperationException("GmCrypto:SM4:KeyHex 未配置"); 
            encoding ??= Encoding.UTF8;
            var crypto = new Sm4Crypto { Encoding = encoding };
            return crypto.DecryptECBFormHex(cipherHex, opt.SM4.KeyHex.Trim());
        }

        private static void ValidateSm4(GmCryptoOptions opt)
        {
            if (opt?.SM4 == null || string.IsNullOrWhiteSpace(opt.SM4.KeyHex) || string.IsNullOrWhiteSpace(opt.SM4.IvHex))
                throw new InvalidOperationException("GmCrypto:SM4:KeyHex 与 IvHex（CBC）均需配置");
        }
    }
}
