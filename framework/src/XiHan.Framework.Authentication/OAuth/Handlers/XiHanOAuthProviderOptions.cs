// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using AspNetOAuthOptions = Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 内置提供商选项基类
/// </summary>
/// <remarks>
/// 基类用别名引用 ASP.NET Core 的 OAuth 选项，避免与框架自己的 <see cref="OAuth.OAuthOptions"/> 同名。
/// 各提供商选项继承本类后不必再处理这层同名。
/// </remarks>
public abstract class XiHanOAuthProviderOptions : AspNetOAuthOptions
{
}
