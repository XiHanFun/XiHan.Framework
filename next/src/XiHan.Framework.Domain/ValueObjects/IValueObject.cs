// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.ValueObjects;

/// <summary>
/// 值对象标记。用 <c>record</c> 声明以获得值相等。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IValueObject;
