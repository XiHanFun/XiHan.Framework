// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 找出配置文件里带值的凭据键
/// </summary>
/// <remarks>
/// 生成 ConfigMap 之前核对：ConfigMap 不加密、读得到它的人多（ADR-0222）。凭据名按 RFC-0014「选项里的凭据」，外加 <c>ConnectionStrings</c> 节；值为空或 <c>null</c> 的是占位，不算。只看 JSON，解析不了的文件不判定
/// </remarks>
internal static partial class ConfigSecretScanner
{
    /// <summary>
    /// 找出一份 JSON 配置里带值的凭据键
    /// </summary>
    /// <remarks>
    /// 返回配置路径（以 <c>:</c> 连接，数组按下标），按出现顺序，不带值
    /// </remarks>
    public static IReadOnlyList<string> Scan(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            var found = new List<string>();
            Walk(document.RootElement, string.Empty, found);
            return found;
        }
    }

    /// <summary>
    /// 深度优先走一遍
    /// </summary>
    private static void Walk(JsonElement element, string path, List<string> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var child = path.Length == 0 ? property.Name : path + ":" + property.Name;
                    // 凭据键下面的整棵子树都算凭据（ConnectionStrings 节下的每个连接串）；只要有一个非空值就报这个键
                    if (CredentialName().IsMatch(property.Name))
                    {
                        if (HasValue(property.Value))
                        {
                            found.Add(child);
                        }

                        continue;
                    }

                    Walk(property.Value, child, found);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, path + ":" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), found);
                    index++;
                }

                break;
        }
    }

    /// <summary>
    /// 是否有非空的值
    /// </summary>
    /// <remarks>
    /// 空串与 <c>null</c> 是占位；数字、布尔照常算值
    /// </remarks>
    private static bool HasValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() is { Length: > 0 },
        JsonValueKind.Object => element.EnumerateObject().Any(static property => HasValue(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(HasValue),
        JsonValueKind.Null or JsonValueKind.Undefined => false,
        _ => true,
    };

    /// <summary>
    /// 凭据名的匹配
    /// </summary>
    /// <remarks>
    /// 配置键不区分大小写；规则同 RFC-0014「选项里的凭据」，外加 <c>ConnectionStrings</c>
    /// </remarks>
    [GeneratedRegex("(Password|Passphrase|Secret|SecretKey|AccessKey|AccessKeyId|ApiKey|PrivateKey|ConnectionStrings?|Credentials?)$|^(Access|Refresh|Bearer|Session)?Token$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialName();
}
