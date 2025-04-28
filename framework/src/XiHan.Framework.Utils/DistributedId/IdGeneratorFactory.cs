#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IdGeneratorFactory
// Guid:d2e74a0b-8f9c-4a71-b1d5-e89f3c706a12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:32:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DistributedId;

/// <summary>
/// ID生成器工厂
/// </summary>
public static class IdGeneratorFactory
{
    // 默认实例
    private static IDistributedIdGenerator? _defaultInstance;

    /// <summary>
    /// 获取默认ID生成器实例
    /// </summary>
    /// <returns>ID生成器</returns>
    public static IDistributedIdGenerator GetInstance()
    {
        if (_defaultInstance != null)
        {
            return _defaultInstance;
        }

        // 使用默认配置
        var options = new IdGeneratorOptions
        {
            WorkerId = 1,
            SeqBitLength = 6,
            WorkerIdBitLength = 6
        };
        _defaultInstance = new SnowflakeIdGenerator(options);
        return _defaultInstance;
    }

    /// <summary>
    /// 设置默认ID生成器实例
    /// </summary>
    /// <param name="generator">ID生成器</param>
    public static void SetInstance(IDistributedIdGenerator generator)
    {
        _defaultInstance = generator;
    }

    /// <summary>
    /// 创建雪花漂移算法ID生成器
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <returns>雪花漂移算法ID生成器</returns>
    public static IDistributedIdGenerator CreateSnowflakeIdGenerator(IdGeneratorOptions options)
    {
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 创建UUID生成器
    /// </summary>
    /// <returns>UUID生成器</returns>
    public static IDistributedIdGenerator CreateUuidGenerator()
    {
        return new UuidGenerator();
    }

    /// <summary>
    /// 创建雪花漂移算法ID生成器，适用于低并发场景
    /// </summary>
    /// <param name="workerId">工作机器ID</param>
    /// <returns>雪花漂移算法ID生成器</returns>
    public static IDistributedIdGenerator CreateLowWorkload(ushort workerId = 1)
    {
        var options = new IdGeneratorOptions
        {
            WorkerId = workerId,
            SeqBitLength = 6,
            WorkerIdBitLength = 6
        };
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 创建雪花漂移算法ID生成器，适用于中等并发场景
    /// </summary>
    /// <param name="workerId">工作机器ID</param>
    /// <returns>雪花漂移算法ID生成器</returns>
    public static IDistributedIdGenerator CreateMediumWorkload(ushort workerId = 1)
    {
        var options = new IdGeneratorOptions
        {
            WorkerId = workerId,
            SeqBitLength = 10,
            WorkerIdBitLength = 6
        };
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 创建雪花漂移算法ID生成器，适用于高并发场景
    /// </summary>
    /// <param name="workerId">工作机器ID</param>
    /// <returns>雪花漂移算法ID生成器</returns>
    public static IDistributedIdGenerator CreateHighWorkload(ushort workerId = 1)
    {
        var options = new IdGeneratorOptions
        {
            WorkerId = workerId,
            SeqBitLength = 12,
            WorkerIdBitLength = 6
        };
        return new SnowflakeIdGenerator(options);
    }
}
