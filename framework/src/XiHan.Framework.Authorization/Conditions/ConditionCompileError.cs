// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 子句的编译错误
/// </summary>
/// <param name="ClauseKey">子句标识</param>
/// <param name="AttributeName">属性名（原文）</param>
/// <param name="Message">错误说明</param>
public sealed record ConditionCompileError(string? ClauseKey, string AttributeName, string Message);
