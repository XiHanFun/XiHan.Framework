// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 批量授予或撤销里一项的结果。
/// </summary>
/// <param name="Grant">该项的授予。</param>
/// <param name="Outcome">仓储的结果，或不存在的失败。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PermissionGrantResult(PermissionGrant Grant, Result Outcome);
