// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 携带凭据的记录在 <c>ToString()</c> 里遮蔽凭据
/// </summary>
/// <remarks>
/// 出处：RFC-0014「机密」，ADR-0086、ADR-0089、ADR-0090。反射找出全部正式包里的公开记录类型（有 <c>PrintMembers</c>），凡有凭据名的属性（口令、密钥、访问密钥 ID、令牌、API 密钥、连接串）或登记的连接串字段，连同同一记录里的用户名属性（登录凭据的身份那一半，ADR-0090），就绕过构造与校验直接写入后备字段：凭据写入哨兵值，其余字符串写入占位值；断言 <c>ToString()</c> 不含哨兵、凭据打印为 <c>***</c>，且每个公开属性都仍然打印（手写的 <c>PrintMembers</c> 不能漏掉新加的属性）。字典与列表类型的属性按记录的缺省规则只打印类型名，不含内容，不在检查之列
/// </remarks>
public sealed partial class CredentialRedactionTests
{
    /// <summary>
    /// 遮蔽文本，与 <c>Redaction.Mask</c>、<c>ConnectionStrings.Mask</c> 相同
    /// </summary>
    private const string Mask = "***";

    /// <summary>
    /// 写进凭据后备字段的哨兵值
    /// </summary>
    private const string Sentinel = "S3ntinel-credential-0123456789abcdef0123456789abcdef";

    /// <summary>
    /// 写进其余字符串后备字段的占位值
    /// </summary>
    /// <remarks>
    /// 以 <c>/</c> 开头：由路径或地址计算的属性（如 <c>HostedBlobUrlOptions.BasePath</c>）按相对路径处理，不会抛出
    /// </remarks>
    private const string Placeholder = "/plain-value";

    /// <summary>
    /// 名字不带凭据词、值却是整串连接串的字段（类型全名与属性名）
    /// </summary>
    /// <remarks>
    /// 与 F4 的脱敏范围一致：连接串整串遮蔽（Redis 配置串、AMQP URI、绑定的主值都可能含口令）
    /// </remarks>
    private static readonly (string Type, string Property)[] _connectionStrings =
    [
        ("XiHan.Framework.Caching.Redis.RedisCachingOptions", "Configuration"),
        ("XiHan.Framework.Messaging.RabbitMQ.RabbitMQMessagingOptions", "Configuration"),
        ("XiHan.Framework.Messaging.Redis.RedisMessagingOptions", "Configuration"),
        ("XiHan.Framework.Kernel.Hosting.BindingValue", "Value"),
    ];

    [Fact]
    public void RecordsCarryingCredentials_MaskThemInToString()
    {
        var failures = new List<string>();
        var checkedTypes = new List<string>();
        foreach (var type in NamingTests.LoadSourceAssemblies().SelectMany(static a => a.GetExportedTypes()).Where(IsRecord).OrderBy(static t => t.FullName, StringComparer.Ordinal))
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(static p => p.GetIndexParameters().Length == 0).ToList();
            var credentials = properties.Where(p => IsCredential(type, p)).ToList();
            if (credentials.Count == 0)
            {
                continue;
            }

            // 用户名只在与凭据同在一个记录里时算凭据：那是一对登录凭据的身份那一半；不带凭据的记录里的用户名（如进程的系统账户）照常打印
            credentials.AddRange(properties.Where(static p => UserName().IsMatch(p.Name)));

            checkedTypes.Add(type.FullName!);
            var instance = RuntimeHelpers.GetUninitializedObject(type);
            if (!Populate(type, instance, credentials, failures))
            {
                continue;
            }

            string text;
            try
            {
                text = instance.ToString()!;
            }
            catch (Exception exception)
            {
                failures.Add($"'{type.FullName}.ToString()' threw {exception.GetType().Name} on the populated instance: {exception.Message}");
                continue;
            }

            if (text.Contains(Sentinel, StringComparison.Ordinal))
            {
                failures.Add($"'{type.FullName}.ToString()' prints a credential in plain text: {text}");
            }

            foreach (var credential in credentials.Where(p => !text.Contains($"{p.Name} = {Mask}", StringComparison.Ordinal)))
            {
                failures.Add($"'{type.FullName}.ToString()' must print '{credential.Name} = {Mask}', but printed: {text}");
            }

            // 手写的 PrintMembers 容易漏掉以后新加的属性：每个公开属性都要照常出现
            foreach (var property in properties.Where(p => !text.Contains($"{p.Name} = ", StringComparison.Ordinal)))
            {
                failures.Add($"'{type.FullName}.ToString()' omits the property '{property.Name}': {text}");
            }
        }

        // 已知至少这些记录带凭据；数目骤减说明扫描坏了，而不是凭据没了
        Assert.True(checkedTypes.Count >= 10, $"Only {checkedTypes.Count} records with credentials were found ({string.Join(", ", checkedTypes)}); the scan looks broken.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 是否为记录类型：有编译器约定的 <c>PrintMembers(StringBuilder)</c>
    /// </summary>
    private static bool IsRecord(Type type)
        => !type.IsInterface && !type.IsAbstract && type.GetMethod("PrintMembers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(StringBuilder)]) is not null;

    /// <summary>
    /// 属性是否为凭据：名字是凭据词，或登记为连接串字段
    /// </summary>
    private static bool IsCredential(Type type, PropertyInfo property)
        => CredentialName().IsMatch(property.Name) || _connectionStrings.Contains((type.FullName!, property.Name));

    /// <summary>
    /// 写入后备字段：凭据写哨兵，其余字符串写占位值
    /// </summary>
    /// <remarks>
    /// 绕过 <c>init</c> 里的校验（密钥长度、地址格式）；找不到后备字段或凭据类型不认识时记一条失败并返回 <see langword="false"/>
    /// </remarks>
    private static bool Populate(Type type, object instance, IReadOnlyList<PropertyInfo> credentials, List<string> failures)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(static f => f.FieldType == typeof(string)))
        {
            field.SetValue(instance, Placeholder);
        }

        foreach (var credential in credentials)
        {
            var field = type.GetField($"<{credential.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field is null)
            {
                failures.Add($"'{type.FullName}.{credential.Name}' has no compiler backing field; extend {nameof(CredentialRedactionTests)} to populate it.");
                return false;
            }

            object? value = field.FieldType == typeof(string) ? Sentinel
                : field.FieldType == typeof(ReadOnlyMemory<byte>) ? new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(Sentinel))
                : field.FieldType == typeof(byte[]) ? Encoding.UTF8.GetBytes(Sentinel)
                : null;
            if (value is null)
            {
                failures.Add($"'{type.FullName}.{credential.Name}' has the unsupported credential type '{field.FieldType}'; extend {nameof(CredentialRedactionTests)} to populate it.");
                return false;
            }

            field.SetValue(instance, value);
        }

        return true;
    }

    /// <summary>
    /// 凭据属性名的正则
    /// </summary>
    /// <remarks>
    /// 口令、密钥、访问密钥 ID（<c>AccessKey</c>、<c>AccessKeyId</c>，访问密钥的一半，ADR-0089）、API 密钥、私钥、连接串、凭据，以及访问 / 刷新 / 会话令牌。分页游标（<c>ContinuationToken</c>）与令牌文件路径（<c>TokenPath</c>）不是凭据；用户名见 <see cref="UserName"/>
    /// </remarks>
    [GeneratedRegex("(Password|Passphrase|Secret|SecretKey|AccessKey|AccessKeyId|ApiKey|PrivateKey|ConnectionString|Credentials?)$|^(Access|Refresh|Bearer|Session)?Token$")]
    private static partial Regex CredentialName();

    /// <summary>
    /// 用户名属性名的正则
    /// </summary>
    /// <remarks>
    /// <c>User</c>、<c>UserName</c>、<c>Username</c>：与凭据同在一个记录里时是登录凭据的身份那一半，一同遮蔽（RFC-0014 规则 2，ADR-0090）
    /// </remarks>
    [GeneratedRegex("^(User|UserName|Username)$")]
    private static partial Regex UserName();
}
