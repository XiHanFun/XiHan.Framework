// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base58 编码和解码（Bitcoin Base58 标准）
/// </summary>
/// <remarks>
/// 符合 Bitcoin Base58 编码标准
/// 主要特点：排除易混字符(0/O/I/l)，高可读性，编码长度较短
/// 常见用途：比特币地址、钱包唯一标识、邀请码、区块链应用
/// 标准参考：https://en.bitcoin.it/wiki/Base58Check_encoding
/// </remarks>
public static class Base58
{
    // Bitcoin Base58 字符表(不含 0, O, I, l 等易混淆字符)
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static readonly int[] Indexes = new int[128];

    static Base58()
    {
        for (var i = 0; i < Indexes.Length; i++)
        {
            Indexes[i] = -1;
        }

        for (var i = 0; i < Alphabet.Length; i++)
        {
            Indexes[Alphabet[i]] = i;
        }
    }

    /// <summary>
    /// 编码 byte 数组为 Base58 字符串
    /// </summary>
    public static string Encode(byte[] input)
    {
        // 原实现直接取 input.Length，null 入参抛的是 NullReferenceException；同组 Base32 已用标准守卫
        ArgumentNullException.ThrowIfNull(input);

        // 原实现把 input 复制进临时缓冲、末尾补一个 0 字节后按"小端有符号"读入，
        // 而 Decode 是按大端写回字节的（result[i] = bytes[bytesLength-1-i]），
        // 两端字节序不一致，多字节输入往返后整体颠倒（{1,2} -> "9r" -> {2,1}），
        // 也与 Bitcoin Base58Check 规定的大端口径不符。
        // 而且前导零单独计数这套写法本身只在大端下成立：只有大端的前导 0x00 不参与数值。
        // 因此统一改为大端无符号读入，顺带省掉防负数用的补零缓冲。
        var intData = new BigInteger(input, isUnsigned: true, isBigEndian: true);

        // 计算前导零的数量
        var leadingZeroCount = 0;
        foreach (var b in input)
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

        if (intData == 0)
        {
            return new string('1', Math.Max(1, leadingZeroCount));
        }

        // 使用 stackalloc 存储结果字符（log58(256) ≈ 1.3657，1.38 已够，另留 1 位余量）
        var maxChars = (int)Math.Ceiling(input.Length * 1.38) + leadingZeroCount + 1;
        var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
        var index = 0;

        // 正向构建（后面会反转）
        while (intData > 0)
        {
            var remainder = (int)(intData % 58);
            resultSpan[index++] = Alphabet[remainder];
            intData /= 58;
        }

        // 添加前导 '1'（Base58 中表示 0）
        for (var i = 0; i < leadingZeroCount; i++)
        {
            resultSpan[index++] = '1';
        }

        // 反转结果
        resultSpan[..index].Reverse();
        return new string(resultSpan[..index]);
    }

    /// <summary>
    /// 解码 Base58 字符串为 byte 数组
    /// </summary>
    public static byte[] Decode(string input)
    {
        // 原实现直接 foreach 入参，null 时抛 NullReferenceException
        ArgumentNullException.ThrowIfNull(input);

        // 计算 Base58 转为大整数
        BigInteger intData = 0;
        foreach (var c in input)
        {
            var digit = Indexes[c];
            if (digit < 0)
            {
                throw new FormatException($"Invalid Base58 character `{c}`");
            }

            intData = (intData * 58) + digit;
        }

        // 转换成 byte[]
        var bytes = intData.ToByteArray();

        // 移除末尾补零
        var bytesLength = bytes.Length;
        if (bytesLength > 0 && bytes[^1] == 0)
        {
            bytesLength--;
        }

        // 计算前导 '1' 的数量（Base58 中的0）
        var leadingZeroCount = 0;
        foreach (var c in input)
        {
            if (c == '1')
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
