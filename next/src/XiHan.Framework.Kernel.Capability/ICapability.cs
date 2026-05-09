// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Capability;

/// <summary>
/// 能力声明。每个扩展包可通过 manifest.json 或实现此接口来声明自己的能力。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface ICapability
{
    /// <summary>
    /// 能力名称（与 NuGet 包名对应）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 能力版本。
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 此能力提供的功能标识列表。
    /// </summary>
    IReadOnlyList<string> Provides { get; }

    /// <summary>
    /// 此能力依赖的其他能力及版本约束。
    /// </summary>
    IReadOnlyDictionary<string, string> Requires { get; }
}

/// <summary>
/// 能力描述符。
/// </summary>
public sealed record CapabilityDescriptor(
    string Name,
    string Version,
    Stability Stability,
    IReadOnlyList<string> Provides,
    IReadOnlyDictionary<string, string> Requires
) : ICapability
{
    IReadOnlyList<string> ICapability.Provides => Provides;
    IReadOnlyDictionary<string, string> ICapability.Requires => Requires;
}
