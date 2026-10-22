// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 批量读取里一项的结果。
/// </summary>
/// <param name="Name">密钥名。</param>
/// <param name="Result">值，或不存在 / 无权限 / 不可达的失败。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SecretLookup(SecretName Name, Result<SecretValue> Result);
