// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Capabilities;

internal sealed class ManifestDocument
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string? Id { get; set; }

    public string? Package { get; set; }

    public string? Version { get; set; }

    public string? Primitives { get; set; }

    public string? Stability { get; set; }

    public string? Kind { get; set; }

    public string? Role { get; set; }

    public string? Isolation { get; set; }

    public List<CapabilityDocument>? Provides { get; set; }

    public List<RequirementDocument>? Requires { get; set; }

    public List<BindingDocument>? Bindings { get; set; }

    public ContributionsDocument? Contributes { get; set; }

    public List<string>? Activation { get; set; }

    public List<string>? Permissions { get; set; }

    public string? Entry { get; set; }
}
