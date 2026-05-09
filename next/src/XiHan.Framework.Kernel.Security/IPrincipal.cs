// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 安全主体。代表当前操作的主体（用户或系统）。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPrincipal
{
    /// <summary>
    /// 主体标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 主体名称。
    /// </summary>
    string Name { get; }
}
