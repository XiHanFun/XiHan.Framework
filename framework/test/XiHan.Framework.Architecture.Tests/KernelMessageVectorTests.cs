// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Architecture.Tests.Model;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 规范向量 <c>kernel-messages.json</c> 与参考实现逐项一致
/// </summary>
/// <remarks>
/// 各语言 SDK 自带的内核错误默认模板以这份向量为准（RFC-0011「错误码」，ADR-0233），所以它必须与 .NET 的错误码常量、消息键常量和 <c>en-US.json</c> 一字不差
/// </remarks>
public sealed class KernelMessageVectorTests
{
    [Fact]
    public void KernelMessages_MatchConstantsAndEnglishCatalogs()
    {
        var vectors = SpecVectors.Read<KernelMessageVectors>("primitives", "kernel-messages.json");
        var codes = Constants(typeof(KernelErrors)).Concat(Constants(typeof(ErrorCode))).ToHashSet(StringComparer.Ordinal);
        var keys = Constants(typeof(KernelMessages)).Concat(Constants(typeof(ResultMessages))).Concat(Constants(typeof(ManifestMessages))).ToHashSet(StringComparer.Ordinal);
        var catalog = EnglishCatalog("XiHan.Framework.Primitives").Concat(EnglishCatalog("XiHan.Framework.Kernel")).ToDictionary(StringComparer.Ordinal);
        var failures = new List<string>();

        Assert.NotEmpty(vectors.Messages);
        foreach (var message in vectors.Messages)
        {
            if (!codes.Contains(message.Code))
            {
                failures.Add($"{message.Key}: code '{message.Code}' is not a constant of 'KernelErrors' or 'ErrorCode'");
            }

            if (!keys.Contains(message.Key))
            {
                failures.Add($"{message.Key}: not a constant of 'KernelMessages', 'ResultMessages' or 'ManifestMessages'");
            }

            if (!catalog.TryGetValue(message.Key, out var template) || template != message.Template)
            {
                failures.Add($"{message.Key}: en-US template is '{template ?? "<missing>"}', the vector has '{message.Template}'");
            }

            var placeholders = MessageTemplate.PlaceholdersOf(message.Template);
            if (!placeholders.SequenceEqual(message.Arguments, StringComparer.Ordinal))
            {
                failures.Add($"{message.Key}: placeholders [{string.Join(", ", placeholders)}] differ from arguments [{string.Join(", ", message.Arguments)}]");
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// 类型上全部字符串常量的值
    /// </summary>
    private static IEnumerable<string> Constants(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!);

    /// <summary>
    /// 一个正式包的 <c>Localization/en-US.json</c>
    /// </summary>
    private static Dictionary<string, string> EnglishCatalog(string project)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Workspace.SourceRoot, project, "Localization", "en-US.json")))!;

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    private sealed record KernelMessageVectors(KernelMessage[] Messages);

    /// <summary>
    /// 一项内核错误：码、消息键、参数名、en-US 模板与适用情形
    /// </summary>
    private sealed record KernelMessage(string Code, string Key, string[] Arguments, string Template, string When);
}
