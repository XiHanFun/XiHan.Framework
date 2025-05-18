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

using System.Text.Json;

namespace XiHan.Framework.DistributedIds;

/// <summary>
/// ID生成器工厂
/// </summary>
public static class IdGeneratorFactory
{
    // 实例缓存
    private static readonly Dictionary<string, IDistributedIdGenerator> _instances = [];

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
    /// 通过名称获取ID生成器实例
    /// </summary>
    /// <param name="name">实例名称</param>
    /// <returns>ID生成器</returns>
    public static IDistributedIdGenerator GetInstanceByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("实例名称不能为空");
        }

        if (_instances.TryGetValue(name, out var instance))
        {
            return instance;
        }

        throw new KeyNotFoundException($"未找到名称为 {name} 的ID生成器实例");
    }

    /// <summary>
    /// 保存ID生成器实例
    /// </summary>
    /// <param name="name">实例名称</param>
    /// <param name="generator">ID生成器</param>
    public static void SaveInstance(string name, IDistributedIdGenerator generator)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("实例名称不能为空");
        }

        _instances[name] = generator;
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
    /// 创建UUID生成器
    /// </summary>
    /// <param name="uuidType">UUID类型</param>
    /// <returns>UUID生成器</returns>
    public static IDistributedIdGenerator CreateUuidGenerator(UuidType uuidType)
    {
        return new UuidGenerator(uuidType);
    }

    /// <summary>
    /// 创建基于名称的UUID生成器
    /// </summary>
    /// <param name="uuidType">UUID类型</param>
    /// <param name="namespaceName">命名空间名称</param>
    /// <param name="name">名称</param>
    /// <returns>UUID生成器</returns>
    public static IDistributedIdGenerator CreateNameBasedUuidGenerator(UuidType uuidType, string namespaceName, string name)
    {
        return new UuidGenerator(uuidType, namespaceName, name);
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

    /// <summary>
    /// 创建短ID生成器（适合URL友好的短ID）
    /// </summary>
    /// <param name="workerId">工作机器ID</param>
    /// <returns>雪花漂移算法ID生成器</returns>
    public static IDistributedIdGenerator CreateShortId(ushort workerId = 1)
    {
        var options = new IdGeneratorOptions
        {
            WorkerId = workerId,
            SeqBitLength = 8,
            WorkerIdBitLength = 4,
            IdLength = 10
        };
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 创建有前缀的ID生成器
    /// </summary>
    /// <param name="prefix">ID前缀</param>
    /// <param name="workerId">工作机器ID</param>
    /// <returns>雪花漂移算法ID生成器</returns>
    public static IDistributedIdGenerator CreatePrefixedId(string prefix, ushort workerId = 1)
    {
        var options = new IdGeneratorOptions
        {
            WorkerId = workerId,
            SeqBitLength = 8,
            WorkerIdBitLength = 6,
            IdPrefix = prefix
        };
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 创建经典雪花算法ID生成器（Twitter Snowflake兼容）
    /// </summary>
    /// <param name="workerId">工作机器ID</param>
    /// <param name="dataCenterId">数据中心ID</param>
    /// <returns>经典雪花算法ID生成器</returns>
    public static IDistributedIdGenerator CreateClassicSnowflake(ushort workerId = 1, byte dataCenterId = 1)
    {
        var options = new IdGeneratorOptions
        {
            WorkerId = workerId,
            DataCenterId = dataCenterId,
            SeqBitLength = 12,
            WorkerIdBitLength = 5,
            DataCenterIdBitLength = 5,
            Method = IdGeneratorOptions.ClassicSnowFlakeMethod
        };
        return new SnowflakeIdGenerator(options);
    }

    /// <summary>
    /// 从JSON配置文件创建ID生成器
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <returns>ID生成器</returns>
    public static IDistributedIdGenerator CreateFromJsonFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("配置文件路径不能为空");
        }

        try
        {
            var jsonString = File.ReadAllText(filePath);
            var config = JsonDocument.Parse(jsonString).RootElement;

            var generatorType = config.GetProperty("GeneratorType").GetString();
            switch (generatorType?.ToLower())
            {
                case "snowflake":
                    var optionsJson = config.GetProperty("Options").GetRawText();
                    var options = JsonSerializer.Deserialize<IdGeneratorOptions>(optionsJson)
                        ?? throw new InvalidOperationException("无法解析Snowflake配置选项");
                    return new SnowflakeIdGenerator(options);

                case "uuid":
                    var uuidTypeStr = config.GetProperty("UuidType").GetString();
                    if (!Enum.TryParse<UuidType>(uuidTypeStr, out var uuidType))
                    {
                        uuidType = UuidType.Standard;
                    }

                    if (uuidType is UuidType.NameBasedMD5 or UuidType.NameBasedSHA1)
                    {
                        var namespaceName = config.GetProperty("Namespace").GetString() ?? "DNS";
                        var name = config.GetProperty("Name").GetString() ?? "default";
                        return new UuidGenerator(uuidType, namespaceName, name);
                    }

                    return new UuidGenerator(uuidType);

                default:
                    throw new NotSupportedException($"不支持的生成器类型：{generatorType}");
            }
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException($"配置文件不存在：{filePath}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"配置文件格式错误：{ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"从配置文件创建生成器失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从环境变量创建ID生成器
    /// </summary>
    /// <returns>ID生成器</returns>
    public static IDistributedIdGenerator CreateFromEnvironment()
    {
        var type = Environment.GetEnvironmentVariable("ID_GENERATOR_TYPE") ?? "snowflake";

        if (type.Equals("uuid", StringComparison.OrdinalIgnoreCase))
        {
            var uuidTypeStr = Environment.GetEnvironmentVariable("ID_GENERATOR_UUID_TYPE") ?? "Standard";
            if (!Enum.TryParse<UuidType>(uuidTypeStr, out var uuidType))
            {
                uuidType = UuidType.Standard;
            }
            return new UuidGenerator(uuidType);
        }
        else // 默认使用雪花算法
        {
            var options = new IdGeneratorOptions();

            // 尝试从环境变量读取配置
            if (ushort.TryParse(Environment.GetEnvironmentVariable("ID_GENERATOR_WORKER_ID"), out var workerId))
            {
                options.WorkerId = workerId;
            }

            if (byte.TryParse(Environment.GetEnvironmentVariable("ID_GENERATOR_DATA_CENTER_ID"), out var dataCenterId))
            {
                options.DataCenterId = dataCenterId;
            }

            if (byte.TryParse(Environment.GetEnvironmentVariable("ID_GENERATOR_METHOD"), out var method))
            {
                options.Method = method;
            }

            return new SnowflakeIdGenerator(options);
        }
    }
}
