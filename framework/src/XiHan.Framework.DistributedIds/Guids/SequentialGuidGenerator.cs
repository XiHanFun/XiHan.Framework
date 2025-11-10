#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SequentialGuidGenerator
// Guid:b535a437-7f12-4979-9621-3471b068ed39
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:36:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Security.Cryptography;

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// 顺序 GUID 生成器
/// 用于生成具有时间顺序性的 GUID
/// 主要特点：
/// 时间顺序性(Sequential)：生成的 GUID 包含时间戳信息，便于排序和索引
/// 兼容性(Compatible)：完全兼容标准 GUID 格式，可直接用于任何需要 GUID 的场景
/// 数据库友好(Database-Friendly)：适合作为数据库主键，能够提高插入性能和索引效率
/// </summary>
public class SequentialGuidGenerator : IDistributedIdGenerator<Guid>
{
    // 静态随机数生成器
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    // 生成器配置
    private readonly SequentialGuidOptions _options;

    // 锁对象
    private readonly Lock _lock = new();

    // 上次生成的 GUID（用于统计）
    private Guid _lastGeneratedGuid;

    // 生成计数器
    private long _generatedCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">生成器配置</param>
    public SequentialGuidGenerator(SequentialGuidOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _lastGeneratedGuid = Guid.Empty;
        _generatedCount = 0;
    }

    /// <summary>
    /// 创建 GUID
    /// </summary>
    /// <param name="guidType">GUID 类型</param>
    /// <returns>GUID</returns>
    public static Guid NextGuid(SequentialGuidType guidType)
    {
        // 使用 ArrayPool 租用字节数组以减少 GC 压力
        // 需要 10 字节随机数 + 8 字节时间戳 + 16 字节 GUID = 34 字节
        var buffer = ArrayPool<byte>.Shared.Rent(34);

        try
        {
            // 使用 Span 提高性能
            var randomBytes = buffer.AsSpan(0, 10);
            var timestampBytes = buffer.AsSpan(10, 8);
            var guidBytes = buffer.AsSpan(26, 16);

            // 生成随机字节
            Rng.GetBytes(randomBytes);

            // 获取当前时间戳（以毫秒为单位）
            var timestamp = DateTime.UtcNow.Ticks / 10000L;

            // 将时间戳写入 Span
            BitConverter.TryWriteBytes(timestampBytes, timestamp);

            // 确保时间戳字节按降序排列
            if (BitConverter.IsLittleEndian)
            {
                timestampBytes.Reverse();
            }

            switch (guidType)
            {
                case SequentialGuidType.SequentialAsString:
                case SequentialGuidType.SequentialAsBinary:

                    // 复制时间戳到 GUID 字节数组
                    timestampBytes.Slice(2, 6).CopyTo(guidBytes);
                    randomBytes.CopyTo(guidBytes.Slice(6, 10));

                    // 如果 GUID 类型为字符串且是小端系统，则反转字节
                    if (guidType == SequentialGuidType.SequentialAsString && BitConverter.IsLittleEndian)
                    {
                        guidBytes.Slice(0, 4).Reverse();
                        guidBytes.Slice(4, 2).Reverse();
                    }

                    break;

                case SequentialGuidType.SequentialAtEnd:

                    // 复制随机数据到 GUID 字节数组
                    randomBytes.CopyTo(guidBytes);
                    timestampBytes.Slice(2, 6).CopyTo(guidBytes.Slice(10, 6));
                    break;
            }

            // 从 Span 创建 GUID
            return new Guid(guidBytes);
        }
        finally
        {
            // 归还数组到池
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 创建 GUID
    /// </summary>
    /// <returns>GUID</returns>
    public Guid NextGuid()
    {
        return NextGuid(_options.GetDefaultSequentialGuidType());
    }

    /// <summary>
    /// 获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    public Guid NextId()
    {
        lock (_lock)
        {
            _lastGeneratedGuid = NextGuid();
            _generatedCount++;
            return _lastGeneratedGuid;
        }
    }

    /// <summary>
    /// 获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    public string NextIdString()
    {
        return NextId().ToString();
    }

    /// <summary>
    /// 批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID数组</returns>
    public Guid[] NextIds(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("批量生成唯一标识的数量必须大于0", nameof(count));
        }

        var ids = new Guid[count];
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
            throw new ArgumentException("批量生成唯一标识的数量必须大于0", nameof(count));
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
    public Task<Guid> NextIdAsync()
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
    public Task<Guid[]> NextIdsAsync(int count)
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
    /// 从 GUID 中提取时间戳
    /// </summary>
    /// <param name="guid">GUID</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(Guid guid)
    {
        // 使用 ArrayPool 租用字节数组以减少 GC 压力
        // 需要 16 字节 GUID + 8 字节时间戳 = 24 字节
        var buffer = ArrayPool<byte>.Shared.Rent(24);

        try
        {
            // 使用 Span 提高性能
            var guidBytes = buffer.AsSpan(0, 16);
            var timestampBytes = buffer.AsSpan(16, 8);

            // 将 GUID 转换为字节数组
            guid.TryWriteBytes(guidBytes);

            var guidType = _options.GetDefaultSequentialGuidType();

            switch (guidType)
            {
                case SequentialGuidType.SequentialAsString:
                case SequentialGuidType.SequentialAsBinary:

                    // 如果是字符串类型且是小端系统，需要反转回来
                    if (guidType == SequentialGuidType.SequentialAsString && BitConverter.IsLittleEndian)
                    {
                        guidBytes.Slice(0, 4).Reverse();
                        guidBytes.Slice(4, 2).Reverse();
                    }

                    // 提取时间戳字节（前6个字节）
                    timestampBytes.Clear();
                    guidBytes.Slice(0, 6).CopyTo(timestampBytes.Slice(2, 6));
                    break;

                case SequentialGuidType.SequentialAtEnd:

                    // 提取时间戳字节（最后6个字节）
                    timestampBytes.Clear();
                    guidBytes.Slice(10, 6).CopyTo(timestampBytes.Slice(2, 6));
                    break;
            }

            // 反转回大端序
            if (BitConverter.IsLittleEndian)
            {
                timestampBytes.Reverse();
            }

            // 转换为时间戳
            var timestamp = BitConverter.ToInt64(timestampBytes);

            // 转换为 DateTime（时间戳是以毫秒为单位）
            return new DateTime(timestamp * 10000);
        }
        finally
        {
            // 归还数组到池
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 从唯一标识中提取工作机器唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>工作机器唯一标识</returns>
    /// <remarks>SequentialGuid 不使用 WorkerId，返回 0</remarks>
    public int ExtractWorkerId(Guid id)
    {
        // SequentialGuid 不使用 WorkerId，返回 0
        return 0;
    }

    /// <summary>
    /// 从唯一标识中提取序列号
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>序列号</returns>
    /// <remarks>SequentialGuid 不使用 Sequence，返回 0</remarks>
    public int ExtractSequence(Guid id)
    {
        // SequentialGuid 不使用 Sequence，返回 0
        return 0;
    }

    /// <summary>
    /// 从唯一标识中提取数据中心唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>数据中心唯一标识</returns>
    /// <remarks>SequentialGuid 不使用 DataCenterId，返回 0</remarks>
    public int ExtractDataCenterId(Guid id)
    {
        // SequentialGuid 不使用 DataCenterId，返回 0
        return 0;
    }

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型</returns>
    public string GetGeneratorType()
    {
        return "SequentialGuid";
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
            { "GuidType", _options.GetDefaultSequentialGuidType().ToString() },
            { "LastGeneratedGuid", _lastGeneratedGuid },
            { "GeneratedCount", _generatedCount }
        };
    }
}
