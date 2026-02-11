#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IdGeneratorFactory
// Guid:d2e74a0b-8f9c-4a71-b1d5-e89f3c706a12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/28 19:32:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.DistributedIds.NanoIds;
using XiHan.Framework.DistributedIds.SnowflakeIds;

namespace XiHan.Framework.DistributedIds;

/// <summary>
/// ID生成器工厂
/// </summary>
public static class IdGeneratorFactory
{
    /// <summary>
    /// 创建雪花漂移算法唯一标识生成器
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <returns>雪花漂移算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator(SnowflakeIdOptions options)
    {
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 创建雪花漂移算法唯一标识生成器，适用于低并发场景
    /// </summary>
    /// <param name="workerId">工作机器唯一标识</param>
    /// <returns>雪花漂移算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator_LowWorkload(ushort workerId = 1)
    {
        return new SnowflakeIdGenerator(SnowflakeIdOptions.LowWorkload(workerId));
    }

    /// <summary>
    /// 创建雪花漂移算法唯一标识生成器，适用于中等并发场景
    /// </summary>
    /// <param name="workerId">工作机器唯一标识</param>
    /// <returns>雪花漂移算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator_MediumWorkload(ushort workerId = 1)
    {
        return new SnowflakeIdGenerator(SnowflakeIdOptions.MediumWorkload(workerId));
    }

    /// <summary>
    /// 创建雪花漂移算法唯一标识生成器，适用于高并发场景
    /// </summary>
    /// <param name="workerId">工作机器唯一标识</param>
    /// <returns>雪花漂移算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator_HighWorkload(ushort workerId = 1)
    {
        return new SnowflakeIdGenerator(SnowflakeIdOptions.HighWorkload(workerId));
    }

    /// <summary>
    /// 创建短唯一标识生成器(适合URL友好的短唯一标识)
    /// </summary>
    /// <param name="workerId">工作机器唯一标识</param>
    /// <returns>雪花漂移算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator_ShortId(ushort workerId = 1)
    {
        return new SnowflakeIdGenerator(SnowflakeIdOptions.ShortId(workerId));
    }

    /// <summary>
    /// 创建有前缀的唯一标识生成器
    /// </summary>
    /// <param name="prefix">ID前缀</param>
    /// <param name="workerId">工作机器唯一标识</param>
    /// <returns>雪花漂移算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator_PrefixedId(string prefix, ushort workerId = 1)
    {
        return new SnowflakeIdGenerator(SnowflakeIdOptions.PrefixedId(prefix, workerId));
    }

    /// <summary>
    /// 创建经典雪花算法唯一标识生成器(Twitter Snowflake兼容)
    /// </summary>
    /// <param name="workerId">工作机器唯一标识</param>
    /// <param name="dataCenterId">数据中心唯一标识</param>
    /// <returns>经典雪花算法唯一标识生成器</returns>
    public static IDistributedIdGenerator<long> CreateSnowflakeIdGenerator_Classic(ushort workerId = 1, byte dataCenterId = 1)
    {
        return new SnowflakeIdGenerator(SnowflakeIdOptions.Classic(workerId, dataCenterId));
    }

    /// <summary>
    /// 创建NanoID生成器
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator(NanoIdOptions options)
    {
        return new NanoIdGenerator(options);
    }

    /// <summary>
    /// 创建数字形式的NanoID生成器
    /// </summary>
    /// <param name="size">长度(默认为10)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_Numeric(int size = 10)
    {
        return new NanoIdGenerator(NanoIdOptions.OnlyNumbers(size));
    }

    /// <summary>
    /// 创建小写字母形式的NanoID生成器
    /// </summary>
    /// <param name="size">长度(默认为16)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_Lowercase(int size = 16)
    {
        return new NanoIdGenerator(NanoIdOptions.OnlyLowercase(size));
    }

    /// <summary>
    /// 创建大写字母形式的NanoID生成器
    /// </summary>
    /// <param name="size">长度(默认为16)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_Uppercase(int size = 16)
    {
        return new NanoIdGenerator(NanoIdOptions.OnlyUppercase(size));
    }

    /// <summary>
    /// 创建URL安全的NanoID生成器
    /// </summary>
    /// <param name="size">长度(默认为21)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_UrlSafe(int size = 21)
    {
        return new NanoIdGenerator(NanoIdOptions.UrlSafe(size));
    }

    /// <summary>
    /// 创建安全字符集的NanoID生成器(无相似字符如：1/I/l, 0/O/o 等)
    /// </summary>
    /// <param name="size">长度(默认为21)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_Safe(int size = 21)
    {
        return new NanoIdGenerator(NanoIdOptions.Safe(size));
    }

    /// <summary>
    /// 创建十六进制字符集的NanoID生成器
    /// </summary>
    /// <param name="size">长度(默认为32)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_Hex(int size = 32)
    {
        return new NanoIdGenerator(NanoIdOptions.Hex(size));
    }

    /// <summary>
    /// 创建自定义字符集的NanoID生成器
    /// </summary>
    /// <param name="alphabet">自定义字符集</param>
    /// <param name="size">长度(默认为21)</param>
    /// <returns>NanoID生成器</returns>
    public static IDistributedIdGenerator<long> CreateNanoIdGenerator_Custom(string alphabet, int size = 21)
    {
        return new NanoIdGenerator(new NanoIdOptions
        {
            Size = size,
            Alphabet = alphabet
        });
    }

    /// <summary>
    /// 创建顺序 GUID 生成器
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <returns>顺序 GUID 生成器</returns>
    public static IDistributedIdGenerator<Guid> CreateSequentialGuidGenerator(SequentialGuidOptions options)
    {
        return new SequentialGuidGenerator(options);
    }

    /// <summary>
    /// 创建默认的顺序 GUID 生成器（末尾形式，适合 SQL Server）
    /// </summary>
    /// <returns>顺序 GUID 生成器</returns>
    public static IDistributedIdGenerator<Guid> CreateSequentialGuidGenerator_Default()
    {
        return new SequentialGuidGenerator(SequentialGuidOptions.Default());
    }

    /// <summary>
    /// 创建字符串形式的顺序 GUID 生成器（适合字符串比较排序）
    /// </summary>
    /// <returns>顺序 GUID 生成器</returns>
    public static IDistributedIdGenerator<Guid> CreateSequentialGuidGenerator_AsString()
    {
        return new SequentialGuidGenerator(SequentialGuidOptions.AsString());
    }

    /// <summary>
    /// 创建二进制形式的顺序 GUID 生成器（适合二进制排序）
    /// </summary>
    /// <returns>顺序 GUID 生成器</returns>
    public static IDistributedIdGenerator<Guid> CreateSequentialGuidGenerator_AsBinary()
    {
        return new SequentialGuidGenerator(SequentialGuidOptions.AsBinary());
    }

    /// <summary>
    /// 创建末尾形式的顺序 GUID 生成器（推荐用于 SQL Server，适合聚集索引）
    /// </summary>
    /// <returns>顺序 GUID 生成器</returns>
    public static IDistributedIdGenerator<Guid> CreateSequentialGuidGenerator_AtEnd()
    {
        return new SequentialGuidGenerator(SequentialGuidOptions.AtEnd());
    }
}
