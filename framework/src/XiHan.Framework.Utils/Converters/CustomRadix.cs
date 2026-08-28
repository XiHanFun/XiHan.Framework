// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// 自定义进制编码器
/// </summary>
/// <remarks>
/// 主要特点：支持自定义字符集和进制，编码长度灵活可控
/// 常见用途：Id 生成器、自定义短码、emoji 编码等
/// </remarks>
public class CustomRadix
{
    private readonly string _alphabet;
    private readonly Dictionary<char, int> _charMap;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="alphabet"></param>
    /// <exception cref="ArgumentException"></exception>
    public CustomRadix(string alphabet)
    {
        if (alphabet.Length < 2)
        {
            throw new ArgumentException("字符集长度必须 >= 2");
        }

        _alphabet = alphabet;
        _charMap = [];
        for (var i = 0; i < _alphabet.Length; i++)
        {
            if (_charMap.ContainsKey(_alphabet[i]))
            {
                throw new ArgumentException($"字符重复: {_alphabet[i]}");
            }

            _charMap[_alphabet[i]] = i;
        }
    }

    /// <summary>
    /// 编码 byte[] 为自定义进制字符串
    /// </summary>
    public string Encode(byte[] data)
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
            return _alphabet[0].ToString();
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

        BigInteger radix = _alphabet.Length;

        // 使用 stackalloc 存储结果字符（预估最大长度）
        // 原公式写的是 Math.Log(_alphabet.Length, 256)，求的是 log_256(基数)，恒小于 1，底数与真数写反了。
        // 1 字节需要的位数是 log_基数(256)：36 字符集下 3 字节实际要 5 位而公式只给 4 位，
        // 二元字符集下 1 字节要 8 位而公式只给 1 位，都会在 resultSpan[index++] 处抛 IndexOutOfRangeException。
        var maxChars = (int)Math.Ceiling(data.Length * Math.Log(256, _alphabet.Length)) + leadingZeroCount + 1;
        var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
        var index = 0;

        // 正向构建（后面会反转）
        while (value > 0)
        {
            var remainder = (int)(value % radix);
            resultSpan[index++] = _alphabet[remainder];
            value /= radix;
        }

        // 添加前导零
        for (var i = 0; i < leadingZeroCount; i++)
        {
            resultSpan[index++] = _alphabet[0];
        }

        // 反转结果
        resultSpan[..index].Reverse();
        return new string(resultSpan[..index]);
    }

    /// <summary>
    /// 解码自定义进制字符串为 byte[]
    /// </summary>
    public byte[] Decode(string encoded)
    {
        // 原实现直接 foreach 入参，null 时抛 NullReferenceException
        ArgumentNullException.ThrowIfNull(encoded);

        BigInteger value = 0;
        BigInteger radix = _alphabet.Length;

        foreach (var c in encoded)
        {
            if (!_charMap.TryGetValue(c, out var valueChar))
            {
                throw new ArgumentException($"非法字符: {c}");
            }

            value = (value * radix) + valueChar;
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
            if (c == _alphabet[0])
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
