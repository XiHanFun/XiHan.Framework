// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 连接串的脱敏。
/// </summary>
/// <remarks>
/// 驱动的异常消息要进错误与健康详情时先经 <see cref="Redact"/>：整串连接串与口令类键（键名含 <c>password</c>、<c>pwd</c>、<c>secret</c>、<c>token</c>、<c>key</c>，不区分大小写）的值换成 <see cref="Mask"/>。主机名、端口、库名与用户名不算机密，驱动消息里有就保留——排查连接问题要用到它们。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConnectionStrings
{
    /// <summary>
    /// 替换机密的掩码。
    /// </summary>
    public const string Mask = "***";

    /// <summary>
    /// 被视为机密的键名片段（键名去掉空白、转小写后包含其一即是）。
    /// </summary>
    private static readonly string[] _secretKeyParts = ["password", "pwd", "secret", "token", "key"];

    /// <summary>
    /// 把文本里的连接串与口令类键的值换成 <see cref="Mask"/>。
    /// </summary>
    /// <remarks>
    /// 连接串按 ADO.NET 的写法解析（<c>键=值;…</c>，值可加引号），解析不了时才按分号与等号逐段切分（引号里的分号会把值切断，只作兜底）。替换区分大小写，长的先换。很短的口令也照换，代价是消息里同样的字符串一并被遮住。
    /// </remarks>
    /// <param name="text">要脱敏的文本（如异常消息）。</param>
    /// <param name="connectionString">连接串。</param>
    public static string Redact(string text, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(connectionString);
        if (text.Length == 0 || string.IsNullOrWhiteSpace(connectionString))
        {
            return text;
        }

        var secrets = new HashSet<string>(StringComparer.Ordinal) { connectionString, connectionString.Trim() };
        foreach (var (key, value) in Parse(connectionString) ?? Split(connectionString))
        {
            if (IsSecretKey(key) && value.Length > 0)
            {
                secrets.Add(value);
            }
        }

        // 长的先换：短口令可能是整串连接串的一部分，先换整串不留下其余片段
        foreach (var secret in secrets.Where(static s => s.Length > 0).OrderByDescending(static s => s.Length))
        {
            text = text.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// 异常的消息链（外层到内层，去重，至多 4 层，以 <c>: </c> 连接），经 <see cref="Redact"/> 脱敏。
    /// </summary>
    /// <remarks>
    /// 驱动常把真正的原因放在内层（如 Npgsql 外层只说连接时出错，内层才是拒绝连接的套接字错误），错误与健康详情的 <c>reason</c> 用它。
    /// </remarks>
    /// <param name="exception">异常。</param>
    /// <param name="connectionString">连接串。</param>
    public static string Describe(Exception exception, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(connectionString);
        var messages = new List<string>();
        for (Exception? current = exception; current is not null && messages.Count < 4; current = current.InnerException)
        {
            var message = current.Message.Trim();
            if (message.Length > 0 && !messages.Exists(m => m.Contains(message, StringComparison.Ordinal)))
            {
                messages.Add(message);
            }
        }

        return Redact(messages.Count == 0 ? exception.GetType().Name : string.Join(": ", messages), connectionString);
    }

    /// <summary>
    /// 键名是否属于口令类。
    /// </summary>
    private static bool IsSecretKey(string key)
    {
        var normalized = string.Concat(key.Where(static c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        return _secretKeyParts.Any(normalized.Contains);
    }

    /// <summary>
    /// 按 ADO.NET 的连接串文法解析出键值（值已去掉引号）。
    /// </summary>
    /// <remarks>
    /// 文法不合法时返回 <see langword="null"/>：由 <see cref="Split"/> 兜住。
    /// </remarks>
    private static List<(string Key, string Value)>? Parse(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var pairs = new List<(string, string)>();
            foreach (string key in builder.Keys)
            {
                pairs.Add((key, builder[key]?.ToString() ?? string.Empty));
            }

            return pairs;
        }
        catch (ArgumentException)
        {
            // 格式不合法的连接串照样要脱敏，交给逐段切分
            return null;
        }
    }

    /// <summary>
    /// 按分号与第一个等号逐段切分出键值（原样与去掉首尾引号两种值）。
    /// </summary>
    private static List<(string Key, string Value)> Split(string connectionString)
    {
        var pairs = new List<(string, string)>();
        foreach (var part in connectionString.Split(';'))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            var key = part[..equals];
            var value = part[(equals + 1)..].Trim();
            pairs.Add((key, value));
            pairs.Add((key, value.Trim('"', '\'', '{', '}')));
        }

        return pairs;
    }
}
