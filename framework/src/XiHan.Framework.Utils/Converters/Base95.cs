// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base95 编码和解码（ASCII 可打印字符标准）
/// </summary>
/// <remarks>
/// 使用 ASCII 可打印字符（32-126，共 95 个字符）
/// 主要特点：最紧凑的标准编码之一，可打印字符集，可读性较差，URL 不完全安全
/// 常见用途：密钥/口令生成(可打印)、二维码/短信传输、嵌入式系统传输数据、空间压缩极致场景、数据序列化压缩格式
/// 标准参考：ASCII 标准（无特定 RFC）
/// </remarks>
public static class Base95
{
    private const int Base = 95;

    // ASCII 可打印字符(从 32 到 126，共 95 个字符)
    // !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~
    // 0-31 ASCII 控制字符
    // 32-126 ASCII 字符
    // 127-255 扩展 ASCII 字符
    private static readonly char[] Alphabet;

    static Base95()
    {
        // 优化：使用直接数组初始化，避免 LINQ
        Alphabet = new char[95];
        for (var i = 0; i < 95; i++)
        {
            Alphabet[i] = (char)(i + 32);
        }
    }

    /// <summary>
    /// 编码 byte[] 为 Base95 字符串
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string Encode(byte[] data)
    {
        // 原实现直接取 data.Length，null 入参抛的是 NullReferenceException；同组 Base32 已用标准守卫
        ArgumentNullException.ThrowIfNull(data);

        // 原实现把 data 复制进临时缓冲、末尾补一个 0 字节后按"小端有符号"读入，
        // 而 Decode 是按大端写回字节的（result[i] = bytes[bytesLength-1-i]），
        // 两端字节序不一致，多字节输入往返后整体颠倒。
        // 而且前导零单独计数这套写法本身只在大端下成立：只有大端的前导 0x00 不参与数值。
        // 因此统一改为大端无符号读入，顺带省掉防负数用的补零缓冲。
        var value = new BigInteger(data, isUnsigned: true, isBigEndian: true);

        if (value == 0)
        {
            return Alphabet[0].ToString();
        }

        // 计算前导零的数量
        var leadingZeroCount = 0;
        foreach (var b in data)
        {
            if (b == 0)
            {
                leadingZeroCount++;
            }
            else
            {
                break;
            }
        }

        // 使用 stackalloc 存储结果字符（log95(256) ≈ 1.2177，1.23 已够，另留 1 位余量）
        var maxChars = (int)Math.Ceiling(data.Length * 1.23) + leadingZeroCount + 1;
        var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
        var index = 0;

        // 正向构建（后面会反转）
        while (value > 0)
        {
            var rem = (int)(value % Base);
            resultSpan[index++] = Alphabet[rem];
            value /= Base;
        }

        // 添加前导零
        for (var i = 0; i < leadingZeroCount; i++)
        {
            resultSpan[index++] = Alphabet[0];
        }

        // 反转结果
        resultSpan[..index].Reverse();
        return new string(resultSpan[..index]);
    }

    /// <summary>
    /// 解码 Base95 字符串为 byte[]
    /// </summary>
    /// <param name="encoded"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static byte[] Decode(string encoded)
    {
        // 原实现直接 foreach 入参，null 时抛 NullReferenceException
        ArgumentNullException.ThrowIfNull(encoded);

        BigInteger value = 0;
        foreach (var c in encoded)
        {
            var index = c - 32;
            if (index is < 0 or >= 95)
            {
                throw new ArgumentException($"非法 Base95 字符: {c}");
            }

            value = (value * Base) + index;
        }

        var bytes = value.ToByteArray();

        // 移除末尾补零
        var bytesLength = bytes.Length;
        if (bytesLength > 0 && bytes[^1] == 0)
        {
            bytesLength--;
        }

        // 计算前导零的数量
        var leadingZeroCount = 0;
        foreach (var c in encoded)
        {
            if (c == Alphabet[0])
            {
                leadingZeroCount++;
            }
            else
            {
                break;
            }
        }

        // 构建最终结果
        var result = new byte[leadingZeroCount + bytesLength];

        // 反转并复制字节（跳过末尾的0）
        for (var i = 0; i < bytesLength; i++)
        {
            result[leadingZeroCount + i] = bytes[bytesLength - 1 - i];
        }

        return result;
    }
}
