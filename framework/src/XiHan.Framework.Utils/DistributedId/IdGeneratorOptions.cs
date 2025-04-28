#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IdGeneratorOptions
// Guid:a6b21358-4c08-4d91-99d6-827d8dcdf311
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:32:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DistributedId;

/// <summary>
/// ID生成器选项
/// </summary>
public class IdGeneratorOptions
{
    /// <summary>
    /// 雪花漂移算法
    /// </summary>
    public const byte SnowFlakeMethod = 1;

    /// <summary>
    /// 传统雪花算法
    /// </summary>
    public const byte ClassicSnowFlakeMethod = 2;

    /// <summary>
    /// 基础时间（默认为2024-01-01）
    /// 不能超过当前系统时间
    /// </summary>
    public DateTime BaseTime { get; set; } = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 机器码
    /// 必须全局唯一
    /// </summary>
    public ushort WorkerId { get; set; } = 0;

    /// <summary>
    /// 机器码位长（默认值6）
    /// 范围：1-15
    /// 机器码与序列号的位数之和不能超过22（64位-42）
    /// </summary>
    public byte WorkerIdBitLength { get; set; } = 6;

    /// <summary>
    /// 序列数位长（默认值6）
    /// 范围：3-21
    /// 机器码与序列号的位数之和不能超过22（64位-42）
    /// </summary>
    public byte SeqBitLength { get; set; } = 6;

    /// <summary>
    /// 最大序列数（含）
    /// 设置范围：0-131071
    /// 默认值63，表示最大序列数是63
    /// </summary>
    public int MaxSeqNumber { get; set; } = 0;

    /// <summary>
    /// 最小序列数（含）
    /// 默认值5，表示最小序列数是5
    /// 设置范围：0-127
    /// </summary>
    public int MinSeqNumber { get; set; } = 5;

    /// <summary>
    /// 最大漂移次数（含）
    /// 默认2000，推荐范围：100-5000
    /// </summary>
    public int TopOverCostCount { get; set; } = 2000;

    /// <summary>
    /// 时间戳类型
    /// 1-秒级，2-毫秒级，默认2
    /// </summary>
    public byte TimestampType { get; set; } = 2;

    /// <summary>
    /// 漂移方法
    /// 1-雪花漂移，2-传统雪花，默认1
    /// </summary>
    public byte Method { get; set; } = SnowFlakeMethod;

    /// <summary>
    /// 数据中心ID(0-31)
    /// 传统雪花算法的数据中心ID，默认0
    /// </summary>
    public byte DataCenterId { get; set; } = 0;
}
