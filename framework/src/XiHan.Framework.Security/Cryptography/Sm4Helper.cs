#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Sm4Helper
// Guid:9a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/14 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Security.Cryptography;

/// <summary>
/// 国密 SM4 对称加密算法辅助类
/// </summary>
/// <remarks>
/// SM4 是我国商用密码标准中的分组密码，分组与密钥长度均为 128 位（16 字节）。
/// 本实现基于 BouncyCastle，采用 CBC 模式 + PKCS7 填充，每次加密使用随机 IV，
/// IV 前置拼在密文头部一同输出；解密时先从头部读回 IV。
/// 与 SM2（非对称）、SM3（摘要）搭配可覆盖"签名 + 摘要 + 对称加密"的全链路国密需求。
/// </remarks>
public static class Sm4Helper
{
    /// <summary>
    /// SM4 分组大小（128 位 = 16 字节），亦即 IV 长度
    /// </summary>
    private const int BlockSizeBytes = 16;

    /// <summary>
    /// SM4 密钥长度（128 位 = 16 字节）
    /// </summary>
    private const int KeySizeBytes = 16;

    /// <summary>
    /// 使用 SM4 加密数据
    /// </summary>
    /// <param name="data">要加密的明文数据</param>
    /// <param name="key">密钥(必须为 16 字节 UTF-8 字符串)</param>
    /// <returns>加密后的数据(Base64 编码，含前置 IV)</returns>
    public static string Encrypt(string data, string key)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var cipherBytes = EncryptBytes(dataBytes, keyBytes);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 使用 SM4 加密数据(字节级别)，输出为 IV(16 字节) + 密文
    /// </summary>
    /// <param name="dataBytes">要加密的明文字节数据</param>
    /// <param name="keyBytes">密钥字节数据(必须为 16 字节)</param>
    /// <returns>加密后的密文字节数据（前 16 字节为随机 IV）</returns>
    public static byte[] EncryptBytes(byte[] dataBytes, byte[] keyBytes)
    {
        // 每次加密生成随机 IV，避免相同明文+密钥产生相同密文
        var iv = RandomNumberGenerator.GetBytes(BlockSizeBytes);
        var cipherBytes = ProcessCipher(dataBytes, keyBytes, iv, true);

        // IV 前置拼接到密文头部
        var result = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);
        return result;
    }

    /// <summary>
    /// 使用 SM4 解密数据
    /// </summary>
    /// <param name="encryptedData">加密的密文数据(Base64 编码，含前置 IV)</param>
    /// <param name="key">密钥(与加密时一致，16 字节)</param>
    /// <returns>解密后的明文数据</returns>
    public static string Decrypt(string encryptedData, string key)
    {
        var cipherBytes = Convert.FromBase64String(encryptedData);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var plainBytes = DecryptBytes(cipherBytes, keyBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// 使用 SM4 解密数据(字节级别)，输入需为 IV(16 字节) + 密文
    /// </summary>
    /// <param name="cipherBytes">加密的密文字节数据（前 16 字节为 IV）</param>
    /// <param name="keyBytes">密钥字节数据(必须为 16 字节)</param>
    /// <returns>解密后的明文字节数据</returns>
    public static byte[] DecryptBytes(byte[] cipherBytes, byte[] keyBytes)
    {
        if (cipherBytes.Length < BlockSizeBytes)
        {
            throw new ArgumentException($"密文长度不足，至少应包含 {BlockSizeBytes} 字节的 IV。", nameof(cipherBytes));
        }

        // 从头部读回 IV，剩余部分才是真正的密文
        var iv = new byte[BlockSizeBytes];
        Buffer.BlockCopy(cipherBytes, 0, iv, 0, BlockSizeBytes);

        var actualCipher = new byte[cipherBytes.Length - BlockSizeBytes];
        Buffer.BlockCopy(cipherBytes, BlockSizeBytes, actualCipher, 0, actualCipher.Length);

        return ProcessCipher(actualCipher, keyBytes, iv, false);
    }

    /// <summary>
    /// 执行加密或解密操作
    /// </summary>
    /// <param name="inputBytes">输入数据(明文或密文)</param>
    /// <param name="keyBytes">密钥字节数据</param>
    /// <param name="iv">初始化向量(16 字节)</param>
    /// <param name="forEncryption">指示是加密 (true) 还是解密 (false)</param>
    /// <returns>处理后的字节数据</returns>
    private static byte[] ProcessCipher(byte[] inputBytes, byte[] keyBytes, byte[] iv, bool forEncryption)
    {
        if (keyBytes.Length != KeySizeBytes)
        {
            throw new ArgumentException($"SM4 密钥长度必须为 {KeySizeBytes} 字节(128 位)。", nameof(keyBytes));
        }

        // 创建 SM4 引擎，CBC 模式 + PKCS7 填充
        var engine = new SM4Engine();
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine));

        // 使用密钥 + IV 初始化
        cipher.Init(forEncryption, new ParametersWithIV(new KeyParameter(keyBytes), iv));

        // 处理数据，并按实际写入长度截断（解密去除填充后长度会小于预估）
        var outputBytes = new byte[cipher.GetOutputSize(inputBytes.Length)];
        var length = cipher.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0);
        length += cipher.DoFinal(outputBytes, length);

        if (length != outputBytes.Length)
        {
            Array.Resize(ref outputBytes, length);
        }

        return outputBytes;
    }
}
