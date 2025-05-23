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

using System.Numerics;
using System.Text;

namespace XiHan.Framework.Utils.System.Converters;

/// <summary>
/// 自定义进制编码器
/// </summary>
/// <remarks>
/// 主要特点：支持自定义字符集和进制，编码长度灵活可控
/// 常见用途：ID 生成器、自定义短码、emoji 编码等
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
        var value = new BigInteger(data.Concat(new byte[] { 0 }).ToArray()); // 防止负数
        if (value == 0)
        {
            return _alphabet[0].ToString();
        }

        var sb = new StringBuilder();
        BigInteger radix = _alphabet.Length;

        while (value > 0)
        {
            var remainder = (int)(value % radix);
            sb.Insert(0, _alphabet[remainder]);
            value /= radix;
        }

        // 前导0保留
        foreach (var b in data)
        {
            if (b == 0)
            {
                sb.Insert(0, _alphabet[0]);
            }
            else
            {
                break;
            }
        }

        return sb.ToString();
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
        if (bytes[^1] == 0)
        {
            bytes = [.. bytes.Take(bytes.Length - 1)];
        }

        // 补前导0
        var leadingZeroCount = encoded.TakeWhile(c => c == _alphabet[0]).Count();
        return [.. Enumerable.Repeat((byte)0, leadingZeroCount), .. bytes.Reverse()];
    }
}
