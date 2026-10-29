// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 的根对象 JSON 形态。
/// </summary>
/// <remarks>
/// 所有字段都是可空的原始文本，缺失与格式错误由 <see cref="ManifestReader"/> 在转换时统一报告。
/// </remarks>
internal sealed class ManifestDocument
{
    /// <summary>
    /// JSON Schema 地址（<c>$schema</c>）。
    /// </summary>
    /// <remarks>
    /// 写出时固定为规范地址，读取时忽略。
    /// </remarks>
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    /// <summary>
    /// 清单 ID。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 所属包 ID。
    /// </summary>
    public string? Package { get; set; }

    /// <summary>
    /// 包版本。
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 依赖的原语层大版本的原始文本。
    /// </summary>
    public string? Primitives { get; set; }

    /// <summary>
    /// 稳定性的小写名称。
    /// </summary>
    public string? Stability { get; set; }

    /// <summary>
    /// 种类的小写名称。
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// 角色的小写名称。
    /// </summary>
    /// <remarks>
    /// 缺省为普通 Provider。
    /// </remarks>
    public string? Role { get; set; }

    /// <summary>
    /// 插件隔离边界：<c>in-process</c>、<c>out-of-process</c> 或 <c>wasm</c>。
    /// </summary>
    public string? Isolation { get; set; }

    /// <summary>
    /// 提供的能力。
    /// </summary>
    public List<CapabilityDocument>? Provides { get; set; }

    /// <summary>
    /// 依赖的能力。
    /// </summary>
    public List<RequirementDocument>? Requires { get; set; }

    /// <summary>
    /// 需要宿主注入的资源。
    /// </summary>
    public List<BindingDocument>? Bindings { get; set; }

    /// <summary>
    /// 贡献点。
    /// </summary>
    public ContributionsDocument? Contributes { get; set; }

    /// <summary>
    /// 激活事件。
    /// </summary>
    /// <remarks>
    /// 缺省为 <c>onStartup</c>。
    /// </remarks>
    public List<string>? Activation { get; set; }

    /// <summary>
    /// 插件所需权限。
    /// </summary>
    public List<string>? Permissions { get; set; }

    /// <summary>
    /// 入口类型的完全限定名。
    /// </summary>
    public string? Entry { get; set; }
}
