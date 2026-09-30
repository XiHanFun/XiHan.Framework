// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 已保存的响应快照
/// </summary>
/// <param name="StatusCode">HTTP 状态码</param>
/// <param name="Body">动作返回值的 JSON（UTF-8），无返回值时为 null</param>
public sealed record StoredResponse(int StatusCode, byte[]? Body);
