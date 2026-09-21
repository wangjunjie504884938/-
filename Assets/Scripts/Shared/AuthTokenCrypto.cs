using System.Text;

namespace ArpgShared
{
    /// <summary>
    /// Token加解密工具 — 客户端(CloudSaveManager)与GameServer(CustomAuthenticator)共用的单一实现
    /// Token以AES加密后存于PlayerPrefs(StorageKey)，发送/使用前必须解密
    /// </summary>
    public static class AuthTokenCrypto
    {
        public const string StorageKey = "ARPG_CloudToken";

        // 16字节AES-128密钥 (旧值"ArpgGuardian2026!!"为18字节导致AES始终抛异常、加密静默失效)
        private static readonly byte[] AesKey = Encoding.UTF8.GetBytes("ArpgGuardian2026");
        // 16字节IV (旧值"InitVector16Bytes"为17字节,同样导致加密失效)
        private static readonly byte[] AesIv = Encoding.UTF8.GetBytes("InitVector16Byte");

        public static string Encrypt(string plainToken)
        {
            if (string.IsNullOrEmpty(plainToken)) return "";
            try
            {
                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Key = AesKey;
                    aes.IV = AesIv;
                    var encryptor = aes.CreateEncryptor();
                    var bytes = Encoding.UTF8.GetBytes(plainToken);
                    var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    return System.Convert.ToBase64String(encrypted);
                }
            }
            catch { return plainToken; }
        }

        public static string Decrypt(string storedToken)
        {
            if (string.IsNullOrEmpty(storedToken)) return "";
            // 兼容旧版明文token: JWT以"eyJ"开头
            if (storedToken.StartsWith("eyJ")) return storedToken;
            try
            {
                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Key = AesKey;
                    aes.IV = AesIv;
                    var decryptor = aes.CreateDecryptor();
                    var bytes = System.Convert.FromBase64String(storedToken);
                    var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
            catch { return ""; }
        }

        /// <summary>从本地加密存储读取并解密Token（仅客户端环境）</summary>
        public static string GetStoredToken()
        {
            return Decrypt(UnityEngine.PlayerPrefs.GetString(StorageKey, ""));
        }
    }
}
