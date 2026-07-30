// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务暴露时操作列表
/// </summary>
public class ServiceExposingActionList : List<Action<IOnServiceExposingContext>>;
