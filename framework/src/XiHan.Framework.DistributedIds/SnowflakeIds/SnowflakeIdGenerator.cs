#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SnowflakeIdGenerator
// Guid:9f4e3d28-a7b6-47c9-81e0-5f9c2d6cf4e8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/28 19:32:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.SnowflakeIds;

/// <summary>
/// 雪花漂移算法唯一标识生成器
/// 用于生成高性能、有序、分布式的长整型唯一标识符(Id)
/// 通常用于分布式系统、微服务架构中需要全局唯一且包含时间顺序信息的场景，如订单号、消息唯一标识等
/// 主要特点：
/// 高性能(High-Performance)：纯内存操作，每秒可生成数百万个唯一标识，无需数据库访问
/// 有序性(Ordered)：ID中包含时间戳，天然有序，便于索引和分区
/// 分布式(Distributed)：通过工作机器唯一标识和数据中心唯一标识区分不同节点，避免冲突
/// 信息丰富(Informative)：可从唯一标识中提取生成时间、工作机器唯一标识、数据中心唯一标识和序列号
/// 算法可选(Versatile)：支持经典雪花算法和雪花漂移算法，满足不同场景需求
/// </summary>
public class SnowflakeIdGenerator : IDistributedIdGenerator<long>
{
    // 生成器配置
    private readonly SnowflakeIdOptions _options;

    // 生成器类型
    private readonly SnowflakeIdTypes _snowflakeIdTypes;

    // 数据中心唯一标识位数
    private readonly byte _dataCenterIdBitLength;

    // 机器码位长
    private readonly byte _workerIdBitLength;

    // 序列号位长
    private readonly byte _seqBitLength;

    // 时间戳位长
    private readonly byte _timestampBitLength;

    // 存储数据中心唯一标识
    private readonly long _dataCenterId;

    // 存储工作进程唯一标识
    private readonly long _workerId;

    // 机器码左移位数
    private readonly int _workerIdShift;

    // 数据中心左移位数
    private readonly int _dataCenterIdShift;

    // 时间戳左移位数
    private readonly int _timestampShift;

    // 时间戳掩码
    private readonly long _timestampMask;

    // 锁对象
    private readonly Lock _lock = new();

    // 最大序列数
    private readonly long _maxSeqNumber;

    // 最小序列数
    private readonly long _minSeqNumber;

    // 最大漂移次数
    private readonly int _topOverCostCount;

    // 基准时间戳
    private readonly long _baseTimestamp;

    // 当前序列号
    private long _currentSeqNumber;

    // 最后时间戳
    private long _lastTimestamp;

    // 漂移次数计数器
    private int _overCostCountInCurrentPeriod;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">生成器配置</param>
    public SnowflakeIdGenerator(SnowflakeIdOptions options)
    {
        _options = options;

        // 1.基础参数
        _dataCenterId = options.DataCenterId;
        _workerId = options.WorkerId;
        _workerIdBitLength = options.WorkerIdBitLength;
        _seqBitLength = options.SeqBitLength;

        // 如果用户没有设置MaxSeqNumber，则使用默认值(2^SeqBitLength-1)
        _maxSeqNumber = options.MaxSeqNumber <= 0 ? (1 << _seqBitLength) - 1 : options.MaxSeqNumber;

        // 最小序列数取默认值或用户设置值
        _minSeqNumber = options.MinSeqNumber;

        // 最大漂移次数
        _topOverCostCount = options.TopOverCostCount;

        // 2.时间戳相关
        if (options.TimestampType == TimestampTypes.Seconds)
        {
            // 32位，最大表示2^32秒=136年
            _timestampBitLength = 32;
            _timestampMask = ~(-1L << _timestampBitLength);
        }
        else
        {
            // 41位，最大表示2^41毫秒=69年
            _timestampBitLength = 41;
            _timestampMask = ~(-1L << _timestampBitLength);
        }

        // 3.位移计算
        _workerIdShift = _seqBitLength;
        _dataCenterIdShift = _seqBitLength + _workerIdBitLength;
        _snowflakeIdTypes = options.SnowflakeIdType;
        if (_snowflakeIdTypes == SnowflakeIdTypes.ClassicSnowFlakeMethod)
        {
            _dataCenterIdBitLength = 5;
            _timestampShift = _seqBitLength + _workerIdBitLength + _dataCenterIdBitLength;
        }
        else
        {
            _timestampShift = _seqBitLength + _workerIdBitLength + _options.DataCenterIdBitLength;
        }

        // 4.计算基准时间戳
        if (options.TimestampType == TimestampTypes.Seconds)
        {
            _baseTimestamp = GetSecondTimestamp(options.BaseTime); // 秒级时间戳
        }
        else
        {
            _baseTimestamp = GetMillisecondTimestamp(options.BaseTime); // 毫秒级时间戳
        }

        // 5.初始化序列号
        _currentSeqNumber = options.MinSeqNumber;

        // 6.校验配置
        ValidateOptions();
    }

    /// <summary>
    /// 获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    public long NextId()
    {
        lock (_lock)
        {
            return _snowflakeIdTypes == SnowflakeIdTypes.ClassicSnowFlakeMethod ? NextClassicId() : NextSnowflakeId();
        }
    }

    /// <summary>
    /// 获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    public string NextIdString()
    {
        var id = NextId().ToString();

        // 添加前缀(如果有)
        if (!string.IsNullOrEmpty(_options.IdPrefix))
        {
            id = _options.IdPrefix + id;
        }

        // 如果设置了唯一标识Length且不为0
        if (_options.IdLength > 0)
        {
            // 如果实际长度大于指定长度，截断
            if (id.Length > _options.IdLength)
            {
                return id[^_options.IdLength..];
            }
            // 如果实际长度小于指定长度，左侧填充0

            if (id.Length < _options.IdLength)
            {
                return id.PadLeft(_options.IdLength, '0');
            }
        }

        return id;
    }

    /// <summary>
    /// 从唯一标识中提取时间戳
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(long id)
    {
        var timestamp = ((id >> _timestampShift) & _timestampMask) + _baseTimestamp;
        return _options.TimestampType == TimestampTypes.Seconds
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime
            : DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
    }

    /// <summary>
    /// 从唯一标识中提取工作机器唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>工作机器唯一标识</returns>
    public int ExtractWorkerId(long id)
    {
        return (int)((id >> _workerIdShift) & ~(-1L << _workerIdBitLength));
    }

    /// <summary>
    /// 从唯一标识中提取序列号
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(long id)
    {
        return (int)(id & ~(-1L << _seqBitLength));
    }

    /// <summary>
    /// 从唯一标识中提取数据中心唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>数据中心唯一标识</returns>
    public int ExtractDataCenterId(long id)
    {
        return (int)((id >> _dataCenterIdShift) & ~(-1L << _dataCenterIdBitLength));
    }

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型</returns>
    public string GetGeneratorType()
    {
        return $"SnowflakeId ({_snowflakeIdTypes})";
    }

    /// <summary>
    /// 获取生成器状态信息
    /// </summary>
    /// <returns>状态信息字典</returns>
    public Dictionary<string, object> GetStats()
    {
        var stats = new Dictionary<string, object>
        {
            { "GeneratorId", _options.GeneratorId },
            { "GeneratorType", GetGeneratorType() },
            { "WorkerId", _workerId },
            { "DataCenterId", _dataCenterId },
            { "LastTimestamp", _lastTimestamp },
            { "CurrentSequence", _currentSeqNumber },
            { "OverCostCount", _overCostCountInCurrentPeriod },
            { "BaseTime", _options.BaseTime },
            { "TimestampType", _options.TimestampType == TimestampTypes.Seconds ? "秒级" : "毫秒级" }
        };
        return stats;
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
    /// 获取秒级时间戳
    /// </summary>
    /// <param name="time">时间</param>
    /// <returns>秒级时间戳</returns>
    private static long GetSecondTimestamp(DateTime time)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(time.ToUniversalTime() - epoch).TotalSeconds;
    }

    /// <summary>
    /// 获取毫秒级时间戳
    /// </summary>
    /// <param name="time">时间</param>
    /// <returns>毫秒级时间戳</returns>
    private static long GetMillisecondTimestamp(DateTime time)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(time.ToUniversalTime() - epoch).TotalMilliseconds;
    }

    /// <summary>
    /// 校验配置
    /// </summary>
    private void ValidateOptions()
    {
        // 检查BaseTime
        if (GetCurrentTimestamp() < _baseTimestamp)
        {
            throw new Exception("基准时间不能超过当前时间");
        }
    }

    /// <summary>
    /// 雪花漂移算法
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    private long NextSnowflakeId()
    {
        var currentTimestamp = GetCurrentTimestamp();

        // 1.当前时间小于上次时间，说明时间回拨
        if (currentTimestamp < _lastTimestamp)
        {
            // 时钟回拨处理策略
            return HandleClockBackwards(currentTimestamp);
        }

        // 2.如果是同一时间，增加序列号
        if (currentTimestamp == _lastTimestamp)
        {
            if (_options.TimestampType == TimestampTypes.Seconds)
            {
                // 秒级时间戳，同一秒内增加序列号
                _currentSeqNumber++;

                if (_currentSeqNumber > _maxSeqNumber)
                {
                    // 序列号处理策略
                    return HandleSequenceOverflow(currentTimestamp);
                }
            }
            else
            {
                // 毫秒级时间戳，同一毫秒内增加序列号
                _currentSeqNumber++;

                if (_currentSeqNumber > _maxSeqNumber)
                {
                    // 序列号处理策略
                    return HandleSequenceOverflow(currentTimestamp);
                }
            }
        }
        else
        {
            // 3.不同时间，重置序列号
            _currentSeqNumber = _minSeqNumber;

            // 重置漂移计数
            if (_overCostCountInCurrentPeriod > 0)
            {
                _overCostCountInCurrentPeriod = 0;
            }
        }

        // 记录最后时间戳
        _lastTimestamp = currentTimestamp;

        // 生成唯一标识
        var result = (((currentTimestamp - _baseTimestamp) & _timestampMask) << _timestampShift) |
                    (_dataCenterId << _dataCenterIdShift) |
                    (_workerId << _workerIdShift) |
                    _currentSeqNumber;
        return result;
    }

    /// <summary>
    /// 处理时钟回拨情况
    /// </summary>
    /// <param name="currentTimestamp">当前时间戳</param>
    /// <returns>生成的唯一标识</returns>
    private long HandleClockBackwards(long currentTimestamp)
    {
        // 计算回拨的时间
        var backwardsTime = _lastTimestamp - currentTimestamp;

        // 如果回拨时间超过最大容忍值，抛出异常
        if (backwardsTime > _options.MaxBackwardToleranceMs)
        {
            throw new Exception($"时钟回拨太多: {backwardsTime}ms，超过了允许的最大值{_options.MaxBackwardToleranceMs}ms");
        }

        // 等待一会儿，期待时钟会恢复正常
        Thread.Sleep(5);

        // 增加漂移计数
        _overCostCountInCurrentPeriod++;

        // 如果漂移次数超过上限，抛出异常
        if (_overCostCountInCurrentPeriod > _topOverCostCount)
        {
            throw new Exception($"时钟回拨次数超过上限: {_topOverCostCount}次");
        }

        // 递归调用直到不再有时钟回拨
        return NextSnowflakeId();
    }

    /// <summary>
    /// 处理序列号溢出情况
    /// </summary>
    /// <param name="currentTimestamp">当前时间戳</param>
    /// <returns>生成的唯一标识</returns>
    private long HandleSequenceOverflow(long currentTimestamp)
    {
        // 如果开启了循环序列
        if (_options.LoopedSequence)
        {
            _currentSeqNumber = _minSeqNumber;
        }
        else
        {
            // 序列号超过上限，等待下一个时间单位
            _currentSeqNumber = _minSeqNumber;
            currentTimestamp = GetNextTimestamp();
        }

        // 记录最后时间戳
        _lastTimestamp = currentTimestamp;

        // 生成唯一标识
        return (((currentTimestamp - _baseTimestamp) & _timestampMask) << _timestampShift) |
               (_dataCenterId << _dataCenterIdShift) |
               (_workerId << _workerIdShift) |
               _currentSeqNumber;
    }

    /// <summary>
    /// 经典雪花算法
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    private long NextClassicId()
    {
        // 获取当前时间戳
        var timestamp = GetCurrentTimestamp();

        // 如果当前时间小于上次时间戳，说明系统时钟回拨
        if (timestamp < _lastTimestamp)
        {
            throw new Exception($"时钟回拨，拒绝生成唯一标识: {_lastTimestamp - timestamp}ms");
        }

        // 如果是同一时间戳，则增加序列号
        if (_lastTimestamp == timestamp)
        {
            _currentSeqNumber = (_currentSeqNumber + 1) & _maxSeqNumber;
            // 同一毫秒的序列数已经达到最大
            if (_currentSeqNumber == 0)
            {
                // 阻塞到下一个毫秒
                timestamp = GetNextTimestamp();
            }
        }
        else
        {
            // 时间戳改变，序列重置
            _currentSeqNumber = _minSeqNumber;
        }

        // 记录最后时间戳
        _lastTimestamp = timestamp;

        // 生成唯一标识
        return (((timestamp - _baseTimestamp) & _timestampMask) << _timestampShift) |
               (_dataCenterId << _dataCenterIdShift) |
               (_workerId << _workerIdShift) |
               _currentSeqNumber;
    }

    /// <summary>
    /// 等待下一个时间戳
    /// </summary>
    /// <returns>下一个时间戳</returns>
    private long GetNextTimestamp()
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp <= _lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }

    /// <summary>
    /// 获取当前时间戳
    /// </summary>
    /// <returns>当前时间戳</returns>
    private long GetCurrentTimestamp()
    {
        return _options.TimestampType == TimestampTypes.Seconds
            ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() & _timestampMask
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & _timestampMask;
    }
}
