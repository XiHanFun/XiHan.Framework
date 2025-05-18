#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UuidGenerator
// Guid:19580578-69cd-4da0-b4d1-6a68ba08538e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:32:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;

namespace XiHan.Framework.DistributedIds.Uuids;

/// <summary>
/// UUID生成器
/// 用于生成全球唯一、标准化的通用唯一标识符
/// 通常用于需要跨系统、跨平台、跨时间保证唯一性的场景，如分布式数据库主键、跨组织数据交换等
/// 主要特点：
/// 全球唯一（Universally Unique）：通过结合时间、空间和随机数保证在全球范围内唯一性。
/// 多种版本（Multiple Versions）：支持基于时间(v1)、基于名称(v3/v5)、纯随机(v4)等多种生成机制。
/// 互操作性（Interoperability）：符合RFC4122规范，与其他系统和语言生成的UUID完全兼容。
/// 无中心化（Decentralized）：无需中央授权机构，任何系统都可以独立生成不冲突的UUID。
/// 可排序选项（Sortable Option）：提供顺序UUID模式，使UUID可按时间顺序排列，便于索引。
/// </summary>
public class UuidGenerator : IDistributedIdGenerator
{
    // 生成器类型
    private readonly UuidTypes _uuidType;

    // 排序UUID使用的起始时间
    private readonly DateTime _sequentialBaseTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // 名称空间（用于基于名称的UUID）
    private readonly Dictionary<string, Guid> _predefinedNamespaces = new()
    {
        { "DNS", new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8") },
        { "URL", new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8") },
        { "OID", new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8") },
        { "X500", new Guid("6ba7b814-9dad-11d1-80b4-00c04fd430c8") }
    };

    // 用于基于名称的UUID的命名空间
    private readonly Guid _namespace;

    // 用于基于名称的UUID的名称
    private readonly string _name;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="uuidType">UUID类型</param>
    public UuidGenerator(UuidTypes uuidType = UuidTypes.Standard)
    {
        _uuidType = uuidType;
        _namespace = _predefinedNamespaces["DNS"];
        _name = "default";
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="uuidType">UUID类型</param>
    /// <param name="namespaceName">命名空间名称(DNS, URL, OID, X500)</param>
    /// <param name="name">名称</param>
    public UuidGenerator(UuidTypes uuidType, string namespaceName, string name)
    {
        _uuidType = uuidType;

        if (!_predefinedNamespaces.TryGetValue(namespaceName, out var value))
        {
            throw new ArgumentException($"命名空间名称无效，应为：DNS, URL, OID, X500之一");
        }

        _namespace = value;
        _name = name ?? "default";
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="uuidType">UUID类型</param>
    /// <param name="namespace">命名空间GUID</param>
    /// <param name="name">名称</param>
    public UuidGenerator(UuidTypes uuidType, Guid @namespace, string name)
    {
        _uuidType = uuidType;
        _namespace = @namespace;
        _name = name ?? "default";
    }

    /// <summary>
    /// 获取下一个ID
    /// </summary>
    /// <returns>生成的UUID</returns>
    public long NextId()
    {
        return BitConverter.ToInt64(GenerateGuid().ToByteArray(), 0);
    }

    /// <summary>
    /// 获取下一个ID（字符串形式）
    /// </summary>
    /// <returns>生成的UUID字符串</returns>
    public string NextIdString()
    {
        return GenerateGuid().ToString("N");
    }

    /// <summary>
    /// 批量获取ID
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID数组</returns>
    public long[] NextIds(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("批量生成ID的数量必须大于0");
        }

        var ids = new long[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = NextId();
        }
        return ids;
    }

    /// <summary>
    /// 批量获取ID（字符串形式）
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID字符串数组</returns>
    public string[] NextIdStrings(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("批量生成ID的数量必须大于0");
        }

        var idStrings = new string[count];
        for (var i = 0; i < count; i++)
        {
            idStrings[i] = NextIdString();
        }
        return idStrings;
    }

    /// <summary>
    /// 异步获取下一个ID
    /// </summary>
    /// <returns>生成的ID</returns>
    public Task<long> NextIdAsync()
    {
        return Task.FromResult(NextId());
    }

    /// <summary>
    /// 异步获取下一个ID（字符串形式）
    /// </summary>
    /// <returns>生成的ID字符串</returns>
    public Task<string> NextIdStringAsync()
    {
        return Task.FromResult(NextIdString());
    }

    /// <summary>
    /// 异步批量获取ID
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID数组</returns>
    public Task<long[]> NextIdsAsync(int count)
    {
        return Task.FromResult(NextIds(count));
    }

    /// <summary>
    /// 异步批量获取ID（字符串形式）
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID字符串数组</returns>
    public Task<string[]> NextIdStringsAsync(int count)
    {
        return Task.FromResult(NextIdStrings(count));
    }

    /// <summary>
    /// 从ID中提取时间戳
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>时间戳</returns>
    public DateTime ExtractTime(long id)
    {
        // 仅顺序UUID和基于时间的UUID包含时间信息
        if (_uuidType == UuidTypes.Sequential)
        {
            var guid = new Guid([.. BitConverter.GetBytes(id), .. new byte[8]]);
            var timestamp = BitConverter.ToInt64(guid.ToByteArray(), 0);
            return _sequentialBaseTime.AddTicks(timestamp);
        }
        else if (_uuidType == UuidTypes.TimeBasedV1)
        {
            var guid = new Guid([.. BitConverter.GetBytes(id), .. new byte[8]]);
            var bytes = guid.ToByteArray();

            // 提取时间戳（需要调整顺序）
            var timeBytes = new byte[8];
            timeBytes[0] = bytes[6];
            timeBytes[1] = bytes[7];
            timeBytes[2] = bytes[4];
            timeBytes[3] = bytes[5];
            timeBytes[4] = bytes[0];
            timeBytes[5] = bytes[1];
            timeBytes[6] = bytes[2];
            timeBytes[7] = bytes[3];

            var timestamp = BitConverter.ToInt64(timeBytes, 0);
            return new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc).AddTicks(timestamp);
        }

        // 其他UUID类型不包含时间信息
        return DateTime.UtcNow;
    }

    /// <summary>
    /// 从ID中提取工作机器ID
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>工作机器ID</returns>
    public int ExtractWorkerId(long id)
    {
        // UUID不包含工作机器ID信息
        return 0;
    }

    /// <summary>
    /// 从ID中提取序列号
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>序列号</returns>
    public int ExtractSequence(long id)
    {
        // UUID不包含序列号信息
        return 0;
    }

    /// <summary>
    /// 从ID中提取数据中心ID
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>数据中心ID</returns>
    public int ExtractDataCenterId(long id)
    {
        // UUID不包含数据中心ID信息
        return 0;
    }

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型</returns>
    public string GetGeneratorType()
    {
        return $"UUID ({_uuidType})";
    }

    /// <summary>
    /// 获取生成器状态信息
    /// </summary>
    /// <returns>状态信息字典</returns>
    public Dictionary<string, object> GetStats()
    {
        var stats = new Dictionary<string, object>
        {
            { "GeneratorType", GetGeneratorType() },
            { "UuidType", _uuidType.ToString() }
        };

        if (_uuidType is UuidTypes.NameBasedMD5 or UuidTypes.NameBasedSHA1)
        {
            stats.Add("Namespace", _namespace);
            stats.Add("Name", _name);
        }

        return stats;
    }

    /// <summary>
    /// 生成基于时间的UUID (v1)
    /// </summary>
    /// <returns>基于时间的UUID</returns>
    private static Guid GenerateGuidV1()
    {
        // 这是一个简化版本，生产环境应该使用更复杂的实现
        var ticks = DateTime.UtcNow.Ticks;
        var bytes = new byte[16];
        var tickBytes = BitConverter.GetBytes(ticks);

        // Version 1 UUID 格式设置
        Array.Copy(tickBytes, 0, bytes, 0, Math.Min(8, tickBytes.Length));

        // 设置版本号(v1)
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x10);

        // 设置变体
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // 填充剩余字节
        var random = new Random();
        for (var i = 8; i < 16; i++)
        {
            if (i != 8) // 跳过已设置的变体字节
            {
                bytes[i] = (byte)random.Next(256);
            }
        }

        return new Guid(bytes);
    }

    /// <summary>
    /// 生成UUID
    /// </summary>
    /// <returns>生成的UUID</returns>
    private Guid GenerateGuid()
    {
        return _uuidType switch
        {
            UuidTypes.Sequential => GenerateSequentialGuid(),
            UuidTypes.TimeBasedV1 => GenerateGuidV1(),
            UuidTypes.NameBasedMD5 => GenerateGuidV3(),
            UuidTypes.RandomV4 => Guid.NewGuid(),
            UuidTypes.NameBasedSHA1 => GenerateGuidV5(),
            _ => Guid.NewGuid()
        };
    }

    /// <summary>
    /// 生成顺序UUID
    /// </summary>
    /// <returns>顺序UUID</returns>
    private Guid GenerateSequentialGuid()
    {
        // 获取当前时间与基准时间的差值（Ticks）
        var ticks = DateTime.UtcNow.Ticks - _sequentialBaseTime.Ticks;

        // 创建一个字节数组存储UUID
        var bytes = new byte[16];

        // 将时间戳转换为字节
        var tickBytes = BitConverter.GetBytes(ticks);

        // 复制时间戳字节到UUID的前8个字节
        Array.Copy(tickBytes, 0, bytes, 0, 8);

        // 剩余字节使用随机数填充
        var randomBytes = new byte[8];
        new Random().NextBytes(randomBytes);
        Array.Copy(randomBytes, 0, bytes, 8, 8);

        return new Guid(bytes);
    }

    /// <summary>
    /// 生成基于名称和MD5的UUID (v3)
    /// </summary>
    /// <returns>基于名称的UUID</returns>
    private Guid GenerateGuidV3()
    {
        var namespaceBytes = _namespace.ToByteArray();
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(_name);
        var combined = new byte[namespaceBytes.Length + nameBytes.Length];

        // 合并命名空间和名称字节
        Array.Copy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
        Array.Copy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);

        // 计算MD5哈希
        var hashBytes = MD5.HashData(combined);

        // 设置版本号(v3)
        hashBytes[6] = (byte)((hashBytes[6] & 0x0F) | 0x30);

        // 设置变体
        hashBytes[8] = (byte)((hashBytes[8] & 0x3F) | 0x80);

        return new Guid(hashBytes);
    }

    /// <summary>
    /// 生成基于名称和SHA1的UUID (v5)
    /// </summary>
    /// <returns>基于名称的UUID</returns>
    private Guid GenerateGuidV5()
    {
        var namespaceBytes = _namespace.ToByteArray();
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(_name);
        var combined = new byte[namespaceBytes.Length + nameBytes.Length];

        // 合并命名空间和名称字节
        Array.Copy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
        Array.Copy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);

        // 计算SHA1哈希
        var hashBytes = SHA1.HashData(combined);
        var uuidBytes = new byte[16];

        // 只取前16个字节的哈希值
        Array.Copy(hashBytes, 0, uuidBytes, 0, 16);

        // 设置版本号(v5)
        uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | 0x50);

        // 设置变体
        uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80);

        return new Guid(uuidBytes);
    }
}
