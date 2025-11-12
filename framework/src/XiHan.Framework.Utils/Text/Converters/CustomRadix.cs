#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CustomRadix
// Guid:095ba19d-cd90-487b-9b3f-c00416b87c2a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/23 20:50:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Numerics;

namespace XiHan.Framework.Utils.Text.Converters;

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
        // 使用 ArrayPool 租用缓冲区
        var tempBuffer = ArrayPool<byte>.Shared.Rent(data.Length + 1);
        try
        {
            // 复制数据并添加0字节防止负数
            data.CopyTo(tempBuffer.AsSpan());
            tempBuffer[data.Length] = 0;

            var value = new BigInteger(tempBuffer.AsSpan(0, data.Length + 1));

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
            var maxChars = (int)Math.Ceiling(data.Length * Math.Log(_alphabet.Length, 256)) + leadingZeroCount + 1;
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
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
        }
    }

    /// <summary>
    /// 解码自定义进制字符串为 byte[]
    /// </summary>
    public byte[] Decode(string encoded)
    {
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
