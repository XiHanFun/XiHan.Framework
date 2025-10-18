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

using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// 顺序 GUID 生成器
/// </summary>
public class SequentialGuidGenerator : IGuidGenerator, ITransientDependency
{
    private static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项</param>
    public SequentialGuidGenerator(IOptions<SequentialGuidOptions> options)
    {
        Options = options.Value;
    }

    /// <summary>
    /// 选项
    /// </summary>
    public SequentialGuidOptions Options { get; }

    /// <summary>
    /// 创建 GUID
    /// </summary>
    /// <param name="guidType">GUID 类型</param>
    /// <returns>GUID</returns>
    public static Guid Create(SequentialGuidType guidType)
    {
        var randomBytes = new byte[10];
        RandomNumberGenerator.GetBytes(randomBytes);

        // 获取当前时间戳（以毫秒为单位）
        var timestamp = DateTime.UtcNow.Ticks / 10000L;

        // 将时间戳转换为字节数组
        var timestampBytes = BitConverter.GetBytes(timestamp);

        // 确保时间戳字节按降序排列
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        var guidBytes = new byte[16];

        switch (guidType)
        {
            case SequentialGuidType.SequentialAsString:
            case SequentialGuidType.SequentialAsBinary:

                // 复制时间戳到 GUID 字节数组
                Buffer.BlockCopy(timestampBytes, 2, guidBytes, 0, 6);
                Buffer.BlockCopy(randomBytes, 0, guidBytes, 6, 10);

                // 如果 GUID 类型为字符串且是小端系统，则反转字节
                if (guidType == SequentialGuidType.SequentialAsString && BitConverter.IsLittleEndian)
                {
                    Array.Reverse(guidBytes, 0, 4);
                    Array.Reverse(guidBytes, 4, 2);
                }

                break;

            case SequentialGuidType.SequentialAtEnd:

                // 复制随机数据到 GUID 字节数组
                Buffer.BlockCopy(randomBytes, 0, guidBytes, 0, 10);
                Buffer.BlockCopy(timestampBytes, 2, guidBytes, 10, 6);
                break;
        }

        return new Guid(guidBytes);
    }

    /// <summary>
    /// 创建 GUID
    /// </summary>
    /// <returns>GUID</returns>
    public Guid Create()
    {
        return Create(Options.GetDefaultSequentialGuidType());
    }
}
