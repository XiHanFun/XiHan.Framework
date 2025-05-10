#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqidsEncoder
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/08 17:42:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using XiHan.Framework.Utils.Maths;

namespace XiHan.Framework.Utils.DistributedId.Sqids;

/// <summary>
/// Sqids编码器
/// </summary>
/// <typeparam name="T">数字类型</typeparam>
public class SqidsEncoder<T> where T : INumber<T>
{
    private readonly SqidsOptions _options;
    private readonly string _alphabet;
    private readonly HashSet<string> _blocklist;

    /// <summary>
    /// 使用默认选项初始化Sqids编码器
    /// </summary>
    public SqidsEncoder() : this(new SqidsOptions())
    {
    }

    /// <summary>
    /// 使用自定义选项初始化Sqids编码器
    /// </summary>
    /// <param name="options">Sqids选项</param>
    public SqidsEncoder(SqidsOptions options)
    {
        // 验证选项
        if (options.Alphabet.Length < 3)
        {
            throw new ArgumentException("字母表长度必须至少为3个字符", nameof(options));
        }

        if (options.Alphabet.Distinct().Count() != options.Alphabet.Length)
        {
            throw new ArgumentException("字母表必须包含唯一字符", nameof(options));
        }

        // 初始化选项
        _options = options;
        _alphabet = ShuffleAlphabet(options.Alphabet);
        _blocklist = [.. options.BlockList.Select(x => x.ToLowerInvariant())];
    }

    /// <summary>
    /// 将数字编码为字符串
    /// </summary>
    /// <param name="numbers">要编码的数字</param>
    /// <returns>编码后的字符串</returns>
    public string Encode(params T[] numbers)
    {
        if (numbers.Length == 0)
        {
            return string.Empty;
        }

        // 检查数字是否有效
        foreach (var num in numbers)
        {
            if (num.CompareTo(T.Zero) < 0)
            {
                throw new ArgumentException("不能编码负数", nameof(numbers));
            }
        }

        return GenerateId(numbers);
    }

    /// <summary>
    /// 将字符串解码为数字数组
    /// </summary>
    /// <param name="id">要解码的字符串</param>
    /// <returns>解码后的数字数组</returns>
    public T[] Decode(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return [];
        }

        // 检查字符串是否只包含字母表中的字符
        foreach (var c in id)
        {
            if (!_alphabet.Contains(c))
            {
                return [];
            }
        }

        var prefix = id[0];
        var alphabetOffset = _alphabet.IndexOf(prefix);

        // 创建解码用的字母表
        var alphabet = _alphabet[alphabetOffset..] + _alphabet[..alphabetOffset];
        var result = new List<T>();
        var offset = 1;

        while (offset < id.Length)
        {
            var separator = alphabet[0];
            var chunk = "";

            // 提取一组数据，直到分隔符或结束
            while (offset < id.Length && id[offset] != separator)
            {
                chunk += id[offset];
                offset++;
            }

            // 提取后缀
            if (chunk.Length > 0)
            {
                result.Add(SqidsEncoder<T>.ToNumber(chunk, alphabet));
            }

            // 跳过分隔符
            offset++;
        }

        return [.. result];
    }

    /// <summary>
    /// 填充ID至指定长度
    /// </summary>
    private static string PadId(string id, int minLength, string alphabet)
    {
        var separator = alphabet[0];

        while (id.Length < minLength)
        {
            id += separator + alphabet[1];
        }

        if (id.Length > minLength)
        {
            id = id[..minLength];
        }

        return id;
    }

    /// <summary>
    /// 将数字转换为ID片段
    /// </summary>
    private static string ToId(T num, string alphabet)
    {
        if (num.Equals(T.Zero))
        {
            return alphabet[0].ToString();
        }

        var id = new StringBuilder();
        var value = num;
        var alphabetLength = T.CreateChecked(alphabet.Length);

        while (value.CompareTo(T.Zero) > 0)
        {
            var remainder = value.Mod(alphabetLength);
            // 将 remainder 转换为 int 用于索引
            var index = Convert.ToInt32(remainder);
            id.Insert(0, alphabet[index]);
            value = value.Div(alphabetLength);
        }

        return id.ToString();
    }

    /// <summary>
    /// 将ID片段转换为数字
    /// </summary>
    private static T ToNumber(string id, string alphabet)
    {
        var num = T.Zero;
        var alphabetLength = T.CreateChecked(alphabet.Length);

        foreach (var c in id)
        {
            var charIndex = alphabet.IndexOf(c);
            var value = T.CreateChecked(charIndex);
            num = num.Mul(alphabetLength).Add(value);
        }

        return num;
    }

    /// <summary>
    /// 生成ID
    /// </summary>
    private string GenerateId(T[] numbers)
    {
        // 若数字为空，直接返回空字符串
        if (numbers.Length == 0)
        {
            return string.Empty;
        }

        // 计算最小ID长度
        var minLength = Math.Max(_options.MinLength, numbers.Length);

        // 尝试生成不在屏蔽列表中的ID
        for (var i = 0; i < _alphabet.Length; i++)
        {
            // 为每次尝试创建新的字母表
            var alphabetOffset = i;
            var alphabet = _alphabet[alphabetOffset..] + _alphabet[..alphabetOffset];
            var id = alphabet[0].ToString();

            // 编码每个数字
            for (var j = 0; j < numbers.Length; j++)
            {
                var numStr = SqidsEncoder<T>.ToId(numbers[j], alphabet[1..]);
                id += numStr + (j < numbers.Length - 1 ? alphabet[0] : "");
            }

            // 如果ID长度不够，使用填充字符填充
            if (id.Length < minLength)
            {
                id = PadId(id, minLength, alphabet);
            }

            // 检查是否包含被屏蔽的词
            if (!ContainsBlocklisted(id))
            {
                return id;
            }
        }

        // 如果所有尝试都失败，返回最后一次尝试的结果
        var fallbackAlphabet = _alphabet;
        var fallbackId = fallbackAlphabet[0].ToString();

        for (var j = 0; j < numbers.Length; j++)
        {
            var numStr = SqidsEncoder<T>.ToId(numbers[j], fallbackAlphabet[1..]);
            fallbackId += numStr + (j < numbers.Length - 1 ? fallbackAlphabet[0] : "");
        }

        if (fallbackId.Length < minLength)
        {
            fallbackId = PadId(fallbackId, minLength, fallbackAlphabet);
        }

        return fallbackId;
    }

    /// <summary>
    /// 检查是否包含屏蔽词
    /// </summary>
    private bool ContainsBlocklisted(string id)
    {
        var lowerId = id.ToLowerInvariant();

        foreach (var word in _blocklist)
        {
            if (lowerId.Contains(word))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 打乱字母表
    /// </summary>
    private string ShuffleAlphabet(string alphabet)
    {
        // 使用Fisher-Yates洗牌算法
        var chars = alphabet.ToCharArray();
        var n = chars.Length;

        // 根据一个简单的散列值来确定打乱的方式
        var hashCode = _options.GetHashCode();
        var rng = new Random(hashCode);

        while (n > 1)
        {
            n--;
            var k = rng.Next(n + 1);
            (chars[n], chars[k]) = (chars[k], chars[n]);
        }

        return new string(chars);
    }
}

/// <summary>
/// 为不支持泛型约束的版本提供的非泛型包装器
/// </summary>
public class SqidsEncoder : SqidsEncoder<Int32Number>
{
    /// <summary>
    /// 使用默认选项初始化Sqids编码器
    /// </summary>
    public SqidsEncoder() : base()
    {
    }

    /// <summary>
    /// 使用自定义选项初始化Sqids编码器
    /// </summary>
    /// <param name="options">Sqids选项</param>
    public SqidsEncoder(SqidsOptions options) : base(options)
    {
    }

    /// <summary>
    /// 将整数编码为字符串
    /// </summary>
    /// <param name="numbers">要编码的整数</param>
    /// <returns>编码后的字符串</returns>
    public string Encode(params int[] numbers)
    {
        var convertedNumbers = numbers.Select(n => (Int32Number)n).ToArray();
        return base.Encode(convertedNumbers);
    }

    /// <summary>
    /// 将字符串解码为整数数组
    /// </summary>
    /// <param name="id">要解码的字符串</param>
    /// <returns>解码后的整数数组</returns>
    public new int[] Decode(string id)
    {
        var result = base.Decode(id);
        return [.. result.Select(n => (int)n)];
    }
}
