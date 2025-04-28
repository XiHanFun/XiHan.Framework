#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SnowflakeIdGenerator
// Guid:9f4e3d28-a7b6-47c9-81e0-5f9c2d6cf4e8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:32:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DistributedId;

/// <summary>
/// 雪花漂移算法ID生成器
/// </summary>
public class SnowflakeIdGenerator : IDistributedIdGenerator
{
    private readonly IdGeneratorOptions _options;

    // 数据中心ID位数
    private readonly byte _dataCenterIdBitLength;

    // 机器码位长
    private readonly byte _workerIdBitLength;

    // 序列号位长
    private readonly byte _seqBitLength;

    // 存储数据中心ID
    private readonly long _dataCenterId;

    // 存储工作进程ID
    private readonly long _workerId;

    // 机器码左移位数
    private readonly int _workerIdShift;

    // 数据中心左移位数
    private readonly int _dataCenterIdShift;

    // 时间戳左移位数
    private readonly int _timestampShift;

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
    public SnowflakeIdGenerator(IdGeneratorOptions options)
    {
        _options = options;

        // 1.基础参数
        _dataCenterId = options.DataCenterId;
        _workerId = options.WorkerId;
        _workerIdBitLength = options.WorkerIdBitLength;
        _seqBitLength = options.SeqBitLength;

        // 如果用户没有设置MaxSeqNumber，则使用默认值（2^SeqBitLength-1）
        _maxSeqNumber = options.MaxSeqNumber <= 0 ? (1 << _seqBitLength) - 1 : options.MaxSeqNumber;

        // 最小序列数取默认值或用户设置值
        _minSeqNumber = options.MinSeqNumber;

        // 最大漂移次数
        _topOverCostCount = options.TopOverCostCount;

        // 2.时间戳相关
        if (options.TimestampType == 1)
        {
            // 32位，最大表示2^32秒=136年
        }
        else
        {
            // 41位，最大表示2^41毫秒=69年
        }

        // 3.位移计算
        _workerIdShift = _seqBitLength;
        _dataCenterIdShift = _seqBitLength + _workerIdBitLength;
        _timestampShift = _seqBitLength + _workerIdBitLength;
        if (options.Method == IdGeneratorOptions.ClassicSnowFlakeMethod)
        {
            _dataCenterIdBitLength = 5;
            _timestampShift = _seqBitLength + _workerIdBitLength + _dataCenterIdBitLength;
        }

        // 4.计算基准时间戳
        if (options.TimestampType == 1)
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
    /// 获取下一个ID
    /// </summary>
    /// <returns>生成的ID</returns>
    public long NextId()
    {
        lock (_lock)
        {
            return _options.Method == IdGeneratorOptions.ClassicSnowFlakeMethod ? NextClassicId() : NextSnowflakeId();
        }
    }

    /// <summary>
    /// 获取下一个ID（字符串形式）
    /// </summary>
    /// <returns>生成的ID字符串</returns>
    public string NextIdString()
    {
        return NextId().ToString();
    }

    /// <summary>
    /// 从ID中提取时间戳
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(long id)
    {
        var timestamp = (id >> _timestampShift) + _baseTimestamp;
        return _options.TimestampType == 1
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime
            : DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
    }

    /// <summary>
    /// 从ID中提取工作机器ID
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>工作机器ID</returns>
    public int ExtractWorkerId(long id)
    {
        return (int)((id >> _workerIdShift) & ~(-1L << _workerIdBitLength));
    }

    /// <summary>
    /// 从ID中提取序列号
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(long id)
    {
        return (int)(id & ~(-1L << _seqBitLength));
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
        // 1.检查BaseTime
        if (GetCurrentTimestamp() < _baseTimestamp)
        {
            throw new Exception("基准时间不能超过当前时间");
        }

        // 2.检查WorkerId
        if (_options.Method == IdGeneratorOptions.ClassicSnowFlakeMethod)
        {
            // 数据中心ID最大值
            var maxDataCenterId = ~(-1L << _dataCenterIdBitLength);
            if (_dataCenterId > maxDataCenterId || _dataCenterId < 0)
            {
                throw new ArgumentException($"数据中心ID必须在0-{maxDataCenterId}之间");
            }

            // 工作机器ID最大值
            var maxWorkerId = ~(-1L << _workerIdBitLength);
            if (_workerId > maxWorkerId || _workerId < 0)
            {
                throw new ArgumentException($"工作机器ID必须在0-{maxWorkerId}之间");
            }
        }
        else
        {
            // 工作机器ID最大值
            var maxWorkerId = ~(-1L << _workerIdBitLength);
            if (_workerId > maxWorkerId || _workerId < 0)
            {
                throw new ArgumentException($"工作机器ID必须在0-{maxWorkerId}之间");
            }
        }

        // 3.检查SeqBitLength和WorkerIdBitLength之和不能超过22位(64-42)
        if (_seqBitLength + _workerIdBitLength > 22)
        {
            throw new ArgumentException("序列号位长与机器码位长之和不能超过22");
        }

        // 4.检查SeqBitLength范围
        if (_seqBitLength is < 3 or > 21)
        {
            throw new ArgumentException("序列号位长必须在3-21之间");
        }

        // 5.检查WorkerIdBitLength范围
        if (_workerIdBitLength is < 1 or > 15)
        {
            throw new ArgumentException("机器码位长必须在1-15之间");
        }

        // 6.检查MaxSeqNumber范围
        if (_maxSeqNumber > (1 << _seqBitLength) - 1 || _maxSeqNumber < 0)
        {
            throw new ArgumentException($"最大序列数不能超过{(1 << _seqBitLength) - 1}或小于0");
        }

        // 7.检查MinSeqNumber范围
        if (_minSeqNumber is < 0 or > 127)
        {
            throw new ArgumentException("最小序列数必须在0-127之间");
        }

        // 8.检查TopOverCostCount
        if (_topOverCostCount is < 0 or > 10000)
        {
            throw new ArgumentException("最大漂移次数必须在0-10000之间");
        }
    }

    /// <summary>
    /// 雪花漂移算法
    /// </summary>
    /// <returns>生成的ID</returns>
    private long NextSnowflakeId()
    {
        var currentTimestamp = GetCurrentTimestamp();

        // 1.当前时间小于上次时间，说明时间回拨
        if (currentTimestamp < _lastTimestamp)
        {
            if (_lastTimestamp - currentTimestamp > 10000)
            {
                throw new Exception($"时钟回拨太多: {_lastTimestamp - currentTimestamp}ms");
            }

            // 时间回退，等待一会
            Thread.Sleep(10);

            _overCostCountInCurrentPeriod++;

            // 如果漂移次数超过上限，抛出异常
            return _overCostCountInCurrentPeriod > _topOverCostCount
                ? throw new Exception($"时钟回拨次数超过上限: {_topOverCostCount}次")
                : NextSnowflakeId();
        }

        // 2.如果是同一时间，增加序列号
        if (currentTimestamp == _lastTimestamp)
        {
            if (_options.TimestampType == 1)
            {
                // 秒级时间戳，同一秒内增加序列号
                _currentSeqNumber++;

                if (_currentSeqNumber > _maxSeqNumber)
                {
                    // 序列号超过上限，等待下一秒
                    _currentSeqNumber = _minSeqNumber;
                    currentTimestamp = GetNextTimestamp();
                }
            }
            else
            {
                // 毫秒级时间戳，同一毫秒内增加序列号
                _currentSeqNumber++;

                if (_currentSeqNumber > _maxSeqNumber)
                {
                    // 序列号超过上限，等待下一毫秒
                    _currentSeqNumber = _minSeqNumber;
                    currentTimestamp = GetNextTimestamp();
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

        // 生成ID
        var result = ((currentTimestamp - _baseTimestamp) << _timestampShift) | (_workerId << _workerIdShift) | _currentSeqNumber;
        return result;
    }

    /// <summary>
    /// 经典雪花算法
    /// </summary>
    /// <returns>生成的ID</returns>
    private long NextClassicId()
    {
        // 获取当前时间戳
        var timestamp = GetCurrentTimestamp();

        // 如果当前时间小于上次时间戳，说明系统时钟回拨
        if (timestamp < _lastTimestamp)
        {
            throw new Exception($"时钟回拨，拒绝生成ID: {_lastTimestamp - timestamp}ms");
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

        // 生成ID
        return ((timestamp - _baseTimestamp) << _timestampShift) | (_dataCenterId << _dataCenterIdShift) | (_workerId << _workerIdShift) | _currentSeqNumber;
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
        return _options.TimestampType == 1 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
