// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base36 编码和解码（0-9A-Z 标准）
/// </summary>
/// <remarks>
/// 使用标准 Base36 字符集（0-9 和 A-Z，共 36 个字符）
/// 主要特点：比 Base16 更短，但不如 Base62/Base64 紧凑，人类可识别，适合数字与字母组合使用，不包含特殊符号，适合用户手输
/// 常见用途：邀请码、用户标识、订单号、编号编码、短链接唯一标识、数字压缩显示等
/// 标准参考：数学进制转换标准（无特定 RFC）
/// </remarks>
public static class Base36
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 编码 byte[] 为 Base36 字符串
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string Encode(byte[] data)
    {
        // 原实现直接取 data.Length，null 入参抛的是 NullReferenceException；
        // 同组的 Base32 已用标准守卫，这里对齐（Base58/Base62/Base95/CustomRadix 同）
        ArgumentNullException.ThrowIfNull(data);

        // 原实现把 data 复制进临时缓冲、末尾补一个 0 字节后按"小端有符号"读入，
        // 而 Decode 是按大端写回字节的（result[i] = bytes[bytesLength-1-i]），
        // 两端字节序不一致，多字节输入往返后整体颠倒（{1,2} -> "76" -> {2,1}）。
        // 而且前导零单独计数这套写法本身只在大端下成立：只有大端的前导 0x00 不参与数值，
        // 才需要在编码结果里单独补 Alphabet[0]、解码时再补回字节。
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

        // 使用 stackalloc 存储结果字符
        // 原公式按 1.4 倍估算，而 log36(256) ≈ 1.5475，2 字节的 0xFFFF 就需要 4 位却只给了 3 位，
        // 会在 resultSpan[index++] 处抛 IndexOutOfRangeException；改用真实扩展率并多留 1 位余量
        var maxChars = (int)Math.Ceiling(data.Length * 1.5475) + leadingZeroCount + 1;
        var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
        var index = 0;

        // 正向构建（后面会反转）
        while (value > 0)
        {
            var rem = (int)(value % 36);
            resultSpan[index++] = Alphabet[rem];
            value /= 36;
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
    /// 解码 Base36 字符串为 byte[]
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
            var index = Alphabet.IndexOf(c);
            if (index == -1)
            {
                throw new ArgumentException($"非法 Base36 字符: {c}");
            }

            value = (value * 36) + index;
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
