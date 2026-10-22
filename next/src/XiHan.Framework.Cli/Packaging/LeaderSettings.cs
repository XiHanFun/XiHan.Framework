// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 领导选举（Kubernetes Lease）。
/// </summary>
internal sealed record LeaderSettings
{
    /// <summary>
    /// Lease 名。
    /// </summary>
    public string Lease { get; set; } = string.Empty;
}
