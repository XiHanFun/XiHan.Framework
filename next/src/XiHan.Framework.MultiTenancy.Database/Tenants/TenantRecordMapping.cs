// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// <see cref="TenantRecord"/> 与 <see cref="TenantInfo"/> 之间的换算，以及属性列的 JSON 序列化（源生成上下文）。
/// </summary>
internal static class TenantRecordMapping
{
    /// <summary>
    /// 没有属性时属性列的值：空 JSON 对象。
    /// </summary>
    public const string EmptyProperties = "{}";

    /// <summary>
    /// 共享的空属性表，避免每个租户各建一个空字典。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _noProperties = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 由存储行建租户信息；属性列不是 JSON 对象时抛出 <see cref="JsonException"/>（数据损坏不伪装成没有属性）。
    /// </summary>
    public static TenantInfo ToTenantInfo(TenantRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new TenantInfo
        {
            Id = record.Id,
            DisplayName = record.DisplayName,
            IsEnabled = record.IsEnabled,
            Properties = DeserializeProperties(record.PropertiesJson),
        };
    }

    /// <summary>
    /// 由租户信息建新行；创建时间与并发戳由持久化子系统在新增时写入。
    /// </summary>
    public static TenantRecord ToRecord(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new TenantRecord
        {
            Id = tenant.Id,
            DisplayName = tenant.DisplayName,
            IsEnabled = tenant.IsEnabled,
            PropertiesJson = SerializeProperties(tenant.Properties),
        };
    }

    /// <summary>
    /// 把租户信息整份写到已有行上（ID 不变）；修改时间与并发戳由持久化子系统在更新时刷新。
    /// </summary>
    public static void Apply(TenantRecord record, TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(tenant);
        record.DisplayName = tenant.DisplayName;
        record.IsEnabled = tenant.IsEnabled;
        record.PropertiesJson = SerializeProperties(tenant.Properties);
    }

    /// <summary>
    /// 属性 → JSON 对象；键原样写出、区分大小写。
    /// </summary>
    public static string SerializeProperties(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (properties.Count == 0)
        {
            return EmptyProperties;
        }

        // 源生成上下文只认 Dictionary<string, string>：只读视图不是它时先拷贝一份
        var dictionary = properties as Dictionary<string, string> ?? new Dictionary<string, string>(properties, StringComparer.Ordinal);
        return JsonSerializer.Serialize(dictionary, TenantPropertiesJsonContext.Default.DictionaryStringString);
    }

    /// <summary>
    /// JSON 对象 → 属性（键区分大小写）；空列视为没有属性，不是对象（含字面量 <c>null</c>）抛出 <see cref="JsonException"/>。
    /// </summary>
    public static IReadOnlyDictionary<string, string> DeserializeProperties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return _noProperties;
        }

        return JsonSerializer.Deserialize(json, TenantPropertiesJsonContext.Default.DictionaryStringString)
            ?? throw new JsonException("The tenant properties column must hold a JSON object, but was 'null'.");
    }
}
