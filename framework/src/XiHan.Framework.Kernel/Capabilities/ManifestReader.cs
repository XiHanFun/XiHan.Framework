// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 与 <see cref="Manifest"/> 的互转
/// </summary>
/// <remarks>
/// JSON 形态由 <c>spec/manifest/manifest.schema.json</c> 定义，读取的检查顺序与写出的排版由 RFC-0002 规定（ADR-0236），各语言逐字一致；读取在形状核对之后按成员名直接取值，写出手写排版，都不用反射
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class ManifestReader
{
    /// <summary>
    /// 清单文件名
    /// </summary>
    public const string FileName = "xihan.manifest.json";

    /// <summary>
    /// 写出的 <c>$schema</c>
    /// </summary>
    private const string SchemaUri = "https://spec.xihanfun.com/manifest/v1.json";

    /// <summary>
    /// 严格的 UTF-8 编码
    /// </summary>
    /// <remarks>
    /// 文本里的孤立代理项不悄悄换成 U+FFFD，按不是 JSON 处理
    /// </remarks>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 从 JSON 文本读取清单
    /// </summary>
    /// <remarks>
    /// 失败都是 <see cref="KernelErrors.InvalidManifest"/>：不是 JSON 为 <c>kernel.manifest.invalid_json</c>（<c>reason</c> 为 <c>$</c>，重复成员时为它的路径），字面量 <c>null</c> 为 <c>kernel.manifest.empty</c>，
    /// 不合 Schema 为 <c>kernel.manifest.invalid</c>（<c>reason</c> 为第一处问题的路径），跨字段约束失败保留 <see cref="Manifest.Validate"/> 的消息键与参数
    /// </remarks>
    public static Result<Manifest> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        byte[] utf8;
        try
        {
            utf8 = _strictUtf8.GetBytes(json);
        }
        catch (EncoderFallbackException)
        {
            return InvalidJson(JsonPath.Root);
        }

        return ReadUtf8(utf8);
    }

    /// <summary>
    /// 从文件读取清单
    /// </summary>
    /// <remarks>
    /// 文件按 UTF-8 读，开头的 UTF-8 BOM 跳过；不是合法 UTF-8 的文件按不是 JSON 处理（RFC-0002，ADR-0236）
    /// </remarks>
    public static Result<Manifest> ReadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestFileMissing, MessageArguments.Of(("path", path)));
        }

        var bytes = File.ReadAllBytes(path);
        var start = bytes.AsSpan().StartsWith("\uFEFF"u8) ? 3 : 0;
        return ReadUtf8(bytes.AsMemory(start));
    }

    /// <summary>
    /// 把清单写为 JSON 文本
    /// </summary>
    /// <remarks>
    /// 排版是规范的一部分（RFC-0002，ADR-0236）：两个空格缩进、换行只用 LF、末尾不换行，成员按 Schema 的顺序，没有值的可选成员不写，列表总写（空列表写 <c>[]</c>）；字符串只转义必须转义的字符
    /// </remarks>
    public static string Write(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var writer = new IndentedWriter();
        writer.StartObject();
        writer.Member("$schema").String(SchemaUri);
        writer.Member("id").String(manifest.Id);
        if (manifest.Package is { } package)
        {
            writer.Member("package").String(package);
        }

        writer.Member("version").String(manifest.Version);
        writer.Member("primitives").String(manifest.Primitives.ToString(CultureInfo.InvariantCulture));
        writer.Member("stability").String(manifest.Stability.ToString().ToLowerInvariant());
        writer.Member("kind").String(manifest.Kind.ToString().ToLowerInvariant());
        writer.Member("role").String(manifest.Role.ToString().ToLowerInvariant());
        if (manifest.Isolation is { } isolation)
        {
            writer.Member("isolation").String(isolation switch
            {
                PluginIsolation.InProcess => "in-process",
                PluginIsolation.OutOfProcess => "out-of-process",
                _ => "wasm",
            });
        }

        writer.Member("provides").Objects(manifest.Provides, static (w, p) =>
        {
            w.Member("id").String(p.Id);
            w.Member("version").String(p.Version.ToString(CultureInfo.InvariantCulture));
        });
        writer.Member("requires").Objects(manifest.Requires, static (w, r) =>
        {
            w.Member("id").String(r.Id);
            w.Member("version").String(r.Version.ToString(CultureInfo.InvariantCulture));
            w.Member("optional").Boolean(r.Optional);
        });
        writer.Member("bindings").Objects(manifest.Bindings, static (w, b) =>
        {
            w.Member("name").String(b.Name);
            w.Member("type").String(b.Type);
            w.Member("required").Boolean(b.Required);
        });
        writer.Member("contributes").StartObject();
        writer.Member("pipeline").Objects(manifest.Contributes.Pipeline, static (w, p) =>
        {
            w.Member("middleware").String(p.Middleware);
            w.Member("stage").String(p.Stage.ToManifestName());
            w.Member("before").Strings(p.Before);
            w.Member("after").Strings(p.After);
        });
        writer.Member("operations").Strings(manifest.Contributes.Operations);
        if (manifest.Contributes.Config is { } config)
        {
            writer.Member("config").StartObject();
            writer.Member("section").String(config.Section);
            if (config.Schema is { } schema)
            {
                writer.Member("schema").String(schema);
            }

            writer.EndObject();
        }

        writer.Member("permissions").Strings(manifest.Contributes.Permissions);
        writer.Member("health").Strings(manifest.Contributes.Health);
        writer.EndObject();
        writer.Member("activation").Strings(manifest.Activation);
        writer.Member("permissions").Strings(manifest.Permissions);
        if (manifest.Entry is { } entry)
        {
            writer.Member("entry").String(entry);
        }

        writer.EndObject();
        return writer.ToString();
    }

    /// <summary>
    /// 从 UTF-8 文本读取清单
    /// </summary>
    /// <remarks>
    /// 顺序：共同的 JSON 读取检查（语法、重复成员）→ 字面量 null → Schema 形状 → 跨字段约束；每一步只报第一处问题
    /// </remarks>
    private static Result<Manifest> ReadUtf8(ReadOnlyMemory<byte> utf8)
    {
        if (JsonText.FindProblem(utf8.Span).TryGetValue(out var problem))
        {
            return InvalidJson(problem.Path);
        }

        using var parsed = JsonDocument.Parse(utf8);
        var root = parsed.RootElement;
        if (root.ValueKind == JsonValueKind.Null)
        {
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestEmpty);
        }

        if (ManifestShape.FindProblem(root) is { } path)
        {
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestInvalid, MessageArguments.Of(("reason", path)));
        }

        // 形状已核对：按成员取值与原语的格式校验不会再失败，失败即两处规则不一致，按缺陷抛出
        var manifest = Convert(root);
        var validation = manifest.Validate();
        return validation.IsSuccess ? manifest : validation.Error!.WithCode(KernelErrors.InvalidManifest);
    }

    /// <summary>
    /// 不是 JSON 的失败结果
    /// </summary>
    private static Error InvalidJson(string path)
        => new(KernelErrors.InvalidManifest, KernelMessages.ManifestInvalidJson, MessageArguments.Of(("reason", path)));

    /// <summary>
    /// 把已核对形状的 JSON 根对象转换为 <see cref="Manifest"/>
    /// </summary>
    /// <remarks>
    /// 形状检查已保证没有未知成员、没有 <c>null</c>、类型与取值都对（成员名区分大小写），这里按成员名直接取值，缺少的可选成员按 Schema 的缺省值
    /// </remarks>
    private static Manifest Convert(JsonElement root)
    {
        var hasContributes = root.TryGetProperty("contributes", out var contributes);
        return new Manifest
        {
            Id = Text(root, "id")!,
            Package = Text(root, "package"),
            Version = Text(root, "version")!,
            Primitives = ParsePositive(Text(root, "primitives")!),
            Stability = ParseEnum<Stability>(Text(root, "stability")!),
            Kind = ParseEnum<ManifestKind>(Text(root, "kind")!),
            Role = Text(root, "role") is { } role ? ParseEnum<ManifestRole>(role) : ManifestRole.Provider,
            Isolation = Text(root, "isolation") switch
            {
                null => null,
                "in-process" => PluginIsolation.InProcess,
                "out-of-process" => PluginIsolation.OutOfProcess,
                _ => PluginIsolation.Wasm,
            },
            Provides = Objects(root, "provides", static p => new CapabilityDeclaration(Text(p, "id")!, ParsePositive(Text(p, "version")!))),
            Requires = Objects(root, "requires", static r => new CapabilityRequirement(Text(r, "id")!, ParsePositive(Text(r, "version")!.TrimStart('^'))) { Optional = Flag(r, "optional") ?? false }),
            Bindings = Objects(root, "bindings", static b => new Binding(Text(b, "name")!, Text(b, "type")!) { Required = Flag(b, "required") ?? true }),
            Contributes = !hasContributes ? Contributions.Empty : new Contributions
            {
                Pipeline = Objects(contributes, "pipeline", static p => new PipelineContribution(Text(p, "middleware")!, PipelineStageExtensions.TryParse(Text(p, "stage"), out var stage) ? stage : default) { Before = Strings(p, "before") ?? [], After = Strings(p, "after") ?? [] }),
                Operations = Strings(contributes, "operations") ?? [],
                Config = contributes.TryGetProperty("config", out var config) ? new ConfigContribution(Text(config, "section")!) { Schema = Text(config, "schema") } : null,
                Permissions = Strings(contributes, "permissions") ?? [],
                Health = Strings(contributes, "health") ?? [],
            },
            // 缺省为 onStartup；写了就至少一项（形状检查已拒绝空数组）
            Activation = Strings(root, "activation") ?? ["onStartup"],
            Permissions = Strings(root, "permissions") ?? [],
            Entry = Text(root, "entry"),
        };
    }

    /// <summary>
    /// 取字符串成员，没有时为 <see langword="null"/>
    /// </summary>
    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetString() : null;

    /// <summary>
    /// 取布尔成员，没有时为 <see langword="null"/>
    /// </summary>
    private static bool? Flag(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetBoolean() : null;

    /// <summary>
    /// 取字符串数组成员，没有时为 <see langword="null"/>
    /// </summary>
    private static List<string>? Strings(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? [.. value.EnumerateArray().Select(static item => item.GetString()!)] : null;

    /// <summary>
    /// 取对象数组成员并逐项转换，没有时为空列表
    /// </summary>
    private static List<T> Objects<T>(JsonElement element, string name, Func<JsonElement, T> convert)
        => element.TryGetProperty(name, out var value) ? [.. value.EnumerateArray().Select(convert)] : [];

    /// <summary>
    /// 解析已核对过的正整数文本
    /// </summary>
    private static int ParsePositive(string text) => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>
    /// 把小写名解析为枚举
    /// </summary>
    /// <remarks>
    /// 只认 <see cref="Write"/> 写出的小写名；形状检查已保证是其中之一
    /// </remarks>
    private static TEnum ParseEnum<TEnum>(string text)
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().First(value => string.Equals(value.ToString().ToLowerInvariant(), text, StringComparison.Ordinal));

    /// <summary>
    /// 按规范排版写 JSON 的写入器
    /// </summary>
    /// <remarks>
    /// 只覆盖清单用到的写法：对象、字符串列表、对象列表、字符串与布尔
    /// </remarks>
    private sealed class IndentedWriter
    {
        /// <summary>
        /// 输出
        /// </summary>
        private readonly StringBuilder _builder = new();

        /// <summary>
        /// 每一层是否已写过成员或项
        /// </summary>
        private readonly Stack<bool> _written = new();

        /// <summary>
        /// 开始一个对象
        /// </summary>
        public IndentedWriter StartObject()
        {
            _builder.Append('{');
            _written.Push(false);
            return this;
        }

        /// <summary>
        /// 结束一个对象
        /// </summary>
        public void EndObject() => Close('}');

        /// <summary>
        /// 写成员名
        /// </summary>
        public IndentedWriter Member(string name)
        {
            BeforeItem();
            JsonStringText.Write(_builder, name);
            _builder.Append(": ");
            return this;
        }

        /// <summary>
        /// 写字符串值
        /// </summary>
        public void String(string value)
        {
            JsonStringText.Write(_builder, value);
        }

        /// <summary>
        /// 写布尔值
        /// </summary>
        public void Boolean(bool value)
        {
            _builder.Append(value ? "true" : "false");
        }

        /// <summary>
        /// 写字符串列表
        /// </summary>
        public void Strings(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                _builder.Append("[]");
                return;
            }

            _builder.Append('[');
            _written.Push(false);
            foreach (var value in values)
            {
                BeforeItem();
                JsonStringText.Write(_builder, value);
            }

            Close(']');
        }

        /// <summary>
        /// 写对象列表
        /// </summary>
        public void Objects<T>(IReadOnlyList<T> values, Action<IndentedWriter, T> members)
        {
            if (values.Count == 0)
            {
                _builder.Append("[]");
                return;
            }

            _builder.Append('[');
            _written.Push(false);
            foreach (var value in values)
            {
                BeforeItem();
                StartObject();
                members(this, value);
                EndObject();
            }

            Close(']');
        }

        /// <inheritdoc />
        public override string ToString() => _builder.ToString();

        /// <summary>
        /// 成员或列表项之前：逗号、换行与缩进
        /// </summary>
        private void BeforeItem()
        {
            if (_written.Pop())
            {
                _builder.Append(',');
            }

            _written.Push(true);
            _builder.Append('\n').Append(' ', _written.Count * 2);
        }

        /// <summary>
        /// 结束一层：有内容时换行并按外层缩进
        /// </summary>
        private void Close(char bracket)
        {
            if (_written.Pop())
            {
                _builder.Append('\n').Append(' ', _written.Count * 2);
            }

            _builder.Append(bracket);
        }
    }
}
