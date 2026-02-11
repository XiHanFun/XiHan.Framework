#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NanoIdGenerator
// Guid:a4c8f9e5-c710-4d59-b8d3-e76a12d8c4f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/15 10:30:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;

namespace XiHan.Framework.DistributedIds.NanoIds;

/// <summary>
/// NanoID生成器
/// 用于生成安全、URL友好、随机的唯一标识符(Id)
/// 通常用于需要较短唯一标识但又不想暴露顺序信息的场景，如API标识符、数据库关联唯一标识、短链接等
/// 主要特点：
/// 安全性(Security)：使用加密安全的随机数生成，保证唯一标识的不可预测性和抗冲突性
/// 紧凑长度(Compact)：比UUID更短，默认21个字符，但可根据需求调整长度
/// 字符友好(URL-Safe)：仅使用URL安全字符(A-Za-z0-9_-)，避免转义问题，适合各类系统
/// 时间可提取(Time-Extractable)：支持从生成的唯一标识中提取创建时间，便于分析和调试
/// </summary>
public class NanoIdGenerator : IDistributedIdGenerator<long>
{
    // 使用随机数生成器
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    // 生成器配置
    private readonly NanoIdOptions _options;

    // 开始时间
    private readonly DateTime _startTime;

    // 锁对象
    private readonly Lock _lock = new();

    // 当前序列号
    private long _sequence;

    // 上次生成唯一标识的时间戳
    private long _lastTimestamp;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">NanoID选项</param>
    public NanoIdGenerator(NanoIdOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _startTime = options.StartTime;
        _sequence = 0;
        _lastTimestamp = GetTimestamp(_options.TimestampType);
    }

    /// <summary>
    /// 获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    public long NextId()
    {
        lock (_lock)
        {
            // 获取当前时间戳
            var timestamp = GetTimestamp(_options.TimestampType);

            // 如果同一时间，则递增序列号
            if (timestamp == _lastTimestamp)
            {
                _sequence++;
            }
            else
            {
                // 不同时间，重置序列号
                _sequence = 0;
                _lastTimestamp = timestamp;
            }

            // 合并时间戳和序列号生成数值型唯一标识
            var id = (timestamp << 22) | (_sequence & 0x3FFFFF);
            return id;
        }
    }

    /// <summary>
    /// 获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    public string NextIdString()
    {
        return GenerateNanoId(_options.Size, _options.Alphabet);
    }

    /// <summary>
    /// 批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID数组</returns>
    public long[] NextIds(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("批量生成唯一标识的数量必须大于0");
        }

        var ids = new long[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = NextId();
        }
        return ids;
    }

    /// <summary>
    /// 批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID字符串数组</returns>
    public string[] NextIdStrings(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("批量生成唯一标识的数量必须大于0");
        }

        var idStrings = new string[count];
        for (var i = 0; i < count; i++)
        {
            idStrings[i] = NextIdString();
        }
        return idStrings;
    }

    /// <summary>
    /// 异步获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    public Task<long> NextIdAsync()
    {
        return Task.FromResult(NextId());
    }

    /// <summary>
    /// 异步获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    public Task<string> NextIdStringAsync()
    {
        return Task.FromResult(NextIdString());
    }

    /// <summary>
    /// 异步批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID数组</returns>
    public Task<long[]> NextIdsAsync(int count)
    {
        return Task.FromResult(NextIds(count));
    }

    /// <summary>
    /// 异步批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID字符串数组</returns>
    public Task<string[]> NextIdStringsAsync(int count)
    {
        return Task.FromResult(NextIdStrings(count));
    }

    /// <summary>
    /// 从唯一标识中提取时间戳
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(long id)
    {
        // 提取时间戳(高42位)
        var timestamp = id >> 22;

        // 根据时间戳类型转换为DateTime
        return _options.TimestampType == TimestampTypes.Seconds ? _startTime.AddSeconds(timestamp) : _startTime.AddMilliseconds(timestamp);
    }

    /// <summary>
    /// 从唯一标识中提取工作机器唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>工作机器唯一标识</returns>
    public int ExtractWorkerId(long id)
    {
        // NanoID不使用WorkerId，返回0
        return 0;
    }

    /// <summary>
    /// 从唯一标识中提取序列号
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(long id)
    {
        // 提取序列号(低22位)
        return (int)(id & 0x3FFFFF);
    }

    /// <summary>
    /// 从唯一标识中提取数据中心唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>数据中心唯一标识</returns>
    public int ExtractDataCenterId(long id)
    {
        // NanoID不使用DataCenterId，返回0
        return 0;
    }

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型</returns>
    public string GetGeneratorType()
    {
        return "NanoId";
    }

    /// <summary>
    /// 获取生成器状态信息
    /// </summary>
    /// <returns>状态信息字典</returns>
    public Dictionary<string, object> GetStats()
    {
        return new Dictionary<string, object>
        {
            { "GeneratorType", GetGeneratorType() },
            { "Size", _options.Size },
            { "Alphabet", _options.Alphabet },
            { "AlphabetSize", _options.Alphabet.Length },
            { "LastTimestamp", _lastTimestamp },
            { "CurrentSequence", _sequence },
            { "StartTime", _startTime },
            { "TimestampType", _options.TimestampType.ToString() }
        };
    }

    /// <summary>
    /// 生成NanoId
    /// </summary>
    /// <param name="size">ID长度</param>
    /// <param name="alphabet">字符集</param>
    /// <returns>生成的唯一标识</returns>
    private static string GenerateNanoId(int size, string alphabet)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        if (string.IsNullOrEmpty(alphabet) || alphabet.Length < 2)
        {
            throw new ArgumentException("Alphabet must contain at least 2 characters.", nameof(alphabet));
        }

        // 创建用于存储结果的字符数组
        var result = new char[size];
        var alphabetLength = alphabet.Length;

        // 创建适合大小的随机字节数组
        // 为每一位唯一标识字符分配一个随机字节
        var randomBytes = new byte[size];
        Rng.GetBytes(randomBytes);

        // 计算生成随机数所需的位数
        // 例如: 如果字符集长度是64，需要6位 (2^6=64)
        var bitsNeeded = (int)Math.Ceiling(Math.Log(alphabetLength, 2));
        // 每个字节能提供的最大位数
        var bitsPerByte = 8;
        // 计算每个随机字节可以生成的字符数
        var charactersPerByte = bitsPerByte / bitsNeeded;

        // 如果位数不足，需要更多随机字节
        if (charactersPerByte < 1)
        {
            // 需要更多随机字节
            randomBytes = new byte[size * 2]; // 保守估计
            Rng.GetBytes(randomBytes);
        }

        // 为每个位置生成一个随机字符
        for (var i = 0; i < size; i++)
        {
            // 为每个位置重新获取随机字节
            var randomByte = randomBytes[i % randomBytes.Length];

            // 使用新的随机字节避免潜在的模式
            if (i > 0 && i % randomBytes.Length == 0)
            {
                Rng.GetBytes(randomBytes);
            }

            // 映射到字符集范围内
            var index = randomByte % alphabetLength;
            result[i] = alphabet[index];
        }

        return new string(result);
    }

    /// <summary>
    /// 获取当前时间戳
    /// </summary>
    /// <param name="timestampType">时间戳类型</param>
    /// <returns>时间戳</returns>
    private long GetTimestamp(TimestampTypes timestampType)
    {
        var utcNow = DateTime.UtcNow;
        return timestampType == TimestampTypes.Seconds
            ? (long)(utcNow - _startTime).TotalSeconds
            : (long)(utcNow - _startTime).TotalMilliseconds;
    }
}
