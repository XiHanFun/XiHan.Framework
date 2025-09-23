#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SnowflakeIdOptions
// Guid:a6b21358-4c08-4d91-99d6-827d8dcdf311
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:32:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;

namespace XiHan.Framework.DistributedIds.SnowflakeIds;

/// <summary>
/// 雪花Id生成器选项
/// </summary>
public class SnowflakeIdOptions
{
    // JSON序列化选项
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    // 数据中心Id位长
    private readonly byte _dataCenterIdBitLength = 5;

    // Id生成长度
    private byte _idLength;

    // Id前缀
    private string _idPrefix = string.Empty;

    // 是否循环使用序列号
    private bool _loopedSequence;

    // 最大时钟回拨容忍时间(毫秒)
    private int _maxBackwardToleranceMs = 10000;

    // 是否使用自定义纪元时间
    private bool _useCustomEpoch = true;

    // 生成器唯一标识
    private string _generatorId = Guid.NewGuid().ToString("N");

    // 基础时间(纪元时间)
    private DateTime _baseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // 工作机器Id
    private ushort _workerId;

    // 工作机器Id位长
    private byte _workerIdBitLength = 6;

    // 序列号位长
    private byte _seqBitLength = 6;

    // 最大序列数
    private int _maxSeqNumber = 63;

    // 最小序列数
    private int _minSeqNumber = 5;

    // 最大漂移次数
    private int _topOverCostCount = 2000;

    // 时间戳类型(1秒级/2毫秒级)
    private TimestampTypes _timestampType = TimestampTypes.Milliseconds;

    // 算法类型(1雪花漂移/2传统雪花)
    private SnowflakeIdTypes _snowflakeIdType = SnowflakeIdTypes.SnowFlakeMethod;

    // 数据中心Id
    private byte _dataCenterId;

    /// <summary>
    /// 基础时间(默认为2024-01-01)
    /// 不能超过当前系统时间
    /// </summary>
    public DateTime BaseTime
    {
        get => _baseTime;
        set
        {
            // 检查基准时间不能超过当前系统时间
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var baseTimestamp = (long)(value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            if (currentTimestamp < baseTimestamp)
            {
                throw new Exception("基准时间不能超过当前时间");
            }
            _baseTime = value;
        }
    }

    /// <summary>
    /// 机器码
    /// 必须全局唯一
    /// </summary>
    public ushort WorkerId
    {
        get => _workerId;
        set
        {
            var maxWorkerId = ~(-1L << _workerIdBitLength);
            if (value > maxWorkerId)
            {
                throw new ArgumentException($"工作机器Id必须在0-{maxWorkerId}之间");
            }
            _workerId = value;
        }
    }

    /// <summary>
    /// 机器码位长(默认值6)
    /// 范围：1-15
    /// 机器码与序列号的位数之和不能超过22(64位-42)
    /// </summary>
    public byte WorkerIdBitLength
    {
        get => _workerIdBitLength;
        set
        {
            if (value is < 1 or > 15)
            {
                throw new ArgumentException("机器码位长必须在1-15之间");
            }

            if (value + _seqBitLength > 22)
            {
                throw new ArgumentException("序列号位长与机器码位长之和不能超过22");
            }
            _workerIdBitLength = value;
        }
    }

    /// <summary>
    /// 序列数位长(默认值6)
    /// 范围：3-21
    /// 机器码与序列号的位数之和不能超过22(64位-42)
    /// </summary>
    public byte SeqBitLength
    {
        get => _seqBitLength;
        set
        {
            if (value is < 3 or > 21)
            {
                throw new ArgumentException("序列号位长必须在3-21之间");
            }

            if (value + _workerIdBitLength > 22)
            {
                throw new ArgumentException("序列号位长与机器码位长之和不能超过22");
            }
            _seqBitLength = value;
        }
    }

    /// <summary>
    /// 最大序列数(含)
    /// 设置范围：0-131071
    /// 默认值63，表示最大序列数是63
    /// </summary>
    public int MaxSeqNumber
    {
        get => _maxSeqNumber;
        set
        {
            var maxValue = (1 << _seqBitLength) - 1;
            if (value < 0 || value > maxValue)
            {
                throw new ArgumentException($"最大序列数不能超过{maxValue}或小于0");
            }
            _maxSeqNumber = value;
        }
    }

    /// <summary>
    /// 最小序列数(含)
    /// 默认值5，表示最小序列数是5
    /// 设置范围：0-127
    /// </summary>
    public int MinSeqNumber
    {
        get => _minSeqNumber;
        set
        {
            if (value is < 0 or > 127)
            {
                throw new ArgumentException("最小序列数必须在0-127之间");
            }
            _minSeqNumber = value;
        }
    }

    /// <summary>
    /// 最大漂移次数(含)
    /// 默认2000，推荐范围：100-5000
    /// </summary>
    public int TopOverCostCount
    {
        get => _topOverCostCount;
        set
        {
            if (value is < 0 or > 10000)
            {
                throw new ArgumentException("最大漂移次数必须在0-10000之间");
            }
            _topOverCostCount = value;
        }
    }

    /// <summary>
    /// 时间戳类型
    /// 1-秒级，2-毫秒级，默认2
    /// </summary>
    public TimestampTypes TimestampType
    {
        get => _timestampType;
        set
        {
            if (value is not TimestampTypes.Seconds and not TimestampTypes.Milliseconds)
            {
                throw new ArgumentException("时间戳类型必须是秒级或毫秒级");
            }
            _timestampType = value;
        }
    }

    /// <summary>
    /// 漂移方法
    /// 1-雪花漂移，2-传统雪花，默认1
    /// </summary>
    public SnowflakeIdTypes SnowflakeIdType
    {
        get => _snowflakeIdType;
        set
        {
            if (value is not SnowflakeIdTypes.SnowFlakeMethod and not SnowflakeIdTypes.ClassicSnowFlakeMethod)
            {
                throw new ArgumentException("算法类型必须是1或2");
            }
            _snowflakeIdType = value;
        }
    }

    /// <summary>
    /// 数据中心Id(0-31)
    /// 传统雪花算法的数据中心Id，默认0
    /// </summary>
    public byte DataCenterId
    {
        get => _dataCenterId;
        set
        {
            var maxDataCenterId = ~(-1L << _dataCenterIdBitLength);
            if (value > maxDataCenterId)
            {
                throw new ArgumentException($"数据中心Id必须在0-{maxDataCenterId}之间");
            }
            _dataCenterId = value;
        }
    }

    /// <summary>
    /// 数据中心Id位长(默认值5)
    /// 范围：1-10
    /// </summary>
    public byte DataCenterIdBitLength { get; set; } = 5;

    /// <summary>
    /// Id生成长度(默认值0表示使用默认长度)
    /// 范围：0或10-20，0表示使用默认长度
    /// 注意：设置了此值后，生成的Id会被截断或填充到指定长度
    /// </summary>
    public byte IdLength
    {
        get => _idLength;
        set
        {
            if (value is not 0 and (< 10 or > 20))
            {
                throw new ArgumentException("Id长度必须为0或10-20之间");
            }
            _idLength = value;
        }
    }

    /// <summary>
    /// Id前缀(可选)
    /// </summary>
    public string IdPrefix
    {
        get => _idPrefix;
        set => _idPrefix = value ?? string.Empty;
    }

    /// <summary>
    /// 循环使用序列号
    /// 如果为true，当序列号到达最大值时会循环回最小值，而不是等待下一毫秒
    /// 默认为false
    /// </summary>
    public bool LoopedSequence
    {
        get => _loopedSequence;
        set => _loopedSequence = value;
    }

    /// <summary>
    /// 最大等待回拨时间(毫秒)
    /// 默认10000ms(10秒)
    /// </summary>
    public int MaxBackwardToleranceMs
    {
        get => _maxBackwardToleranceMs;
        set
        {
            if (value is < 0 or > 60000)
            {
                throw new ArgumentException("最大时钟回拨容忍时间必须在0-60000毫秒之间");
            }
            _maxBackwardToleranceMs = value;
        }
    }

    /// <summary>
    /// 使用自定义纪元时间
    /// 如果为true，会使用BaseTime作为时间起点
    /// 如果为false，使用标准雪花算法的2020年1月1日作为起点
    /// </summary>
    public bool UseCustomEpoch
    {
        get => _useCustomEpoch;
        set => _useCustomEpoch = value;
    }

    /// <summary>
    /// 生成器标识，用于标识不同的生成器实例
    /// </summary>
    public string GeneratorId
    {
        get => _generatorId;
        set => _generatorId = value;
    }

    /// <summary>
    /// 低负载模式
    /// </summary>
    /// <param name="workerId">工作机器Id</param>
    /// <returns>配置对象</returns>
    public static SnowflakeIdOptions LowWorkload(ushort workerId = 1)
    {
        return new SnowflakeIdOptions
        {
            WorkerId = workerId,
            SeqBitLength = 6,
            WorkerIdBitLength = 6
        };
    }

    /// <summary>
    /// 中负载模式
    /// </summary>
    /// <param name="workerId">工作机器Id</param>
    /// <returns>配置对象</returns>
    public static SnowflakeIdOptions MediumWorkload(ushort workerId = 1)
    {
        return new SnowflakeIdOptions
        {
            WorkerId = workerId,
            SeqBitLength = 10,
            WorkerIdBitLength = 6
        };
    }

    /// <summary>
    /// 高负载模式
    /// </summary>
    /// <param name="workerId">工作机器Id</param>
    /// <returns>配置对象</returns>
    public static SnowflakeIdOptions HighWorkload(ushort workerId = 1)
    {
        return new SnowflakeIdOptions
        {
            WorkerId = workerId,
            SeqBitLength = 12,
            WorkerIdBitLength = 6
        };
    }

    /// <summary>
    /// 短Id模式
    /// </summary>
    /// <param name="workerId">工作机器Id</param>
    /// <returns>配置对象</returns>
    public static SnowflakeIdOptions ShortId(ushort workerId = 1)
    {
        return new SnowflakeIdOptions
        {
            WorkerId = workerId,
            SeqBitLength = 8,
            WorkerIdBitLength = 4,
            IdLength = 10
        };
    }

    /// <summary>
    /// 带前缀的Id模式
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <param name="workerId">工作机器Id</param>
    /// <returns>配置对象</returns>
    public static SnowflakeIdOptions PrefixedId(string prefix, ushort workerId = 1)
    {
        return new SnowflakeIdOptions
        {
            WorkerId = workerId,
            SeqBitLength = 8,
            WorkerIdBitLength = 6,
            IdPrefix = prefix
        };
    }

    /// <summary>
    /// 经典雪花算法模式
    /// </summary>
    /// <param name="workerId">工作机器Id</param>
    /// <param name="dataCenterId">数据中心Id</param>
    /// <returns>配置对象</returns>
    public static SnowflakeIdOptions Classic(ushort workerId = 1, byte dataCenterId = 1)
    {
        return new SnowflakeIdOptions
        {
            WorkerId = workerId,
            DataCenterId = dataCenterId,
            SeqBitLength = 12,
            WorkerIdBitLength = 5,
            DataCenterIdBitLength = 5,
            SnowflakeIdType = SnowflakeIdTypes.ClassicSnowFlakeMethod
        };
    }

    /// <summary>
    /// 从JSON字符串加载配置
    /// </summary>
    /// <param name="json">JSON配置字符串</param>
    /// <returns>加载后的配置对象</returns>
    public static SnowflakeIdOptions FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentException("JSON字符串不能为空");
        }

        try
        {
            return JsonSerializer.Deserialize<SnowflakeIdOptions>(json)
                ?? throw new InvalidOperationException("无法反序列化JSON到IdGeneratorOptions");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"从JSON加载配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 转换为JSON字符串
    /// </summary>
    /// <returns>JSON字符串</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, CachedJsonSerializerOptions);
    }

    /// <summary>
    /// 克隆当前配置
    /// </summary>
    /// <returns>克隆的配置对象</returns>
    public SnowflakeIdOptions Clone()
    {
        return this;
    }
}
