#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOptionsFactory
// Guid:d2199d7d-ee37-4bfe-94d5-79e74511e23d
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-28 上午 10:42:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Core.Options;

/// <summary>
/// 选项工厂
/// </summary>
/// <typeparam name="TOptions"></typeparam>
public class XiHanOptionsFactory<TOptions> : IOptionsFactory<TOptions> where TOptions : class, new()
{
    private readonly IConfigureOptions<TOptions>[] _setups;
    private readonly IPostConfigureOptions<TOptions>[] _postConfigures;
    private readonly IValidateOptions<TOptions>[] _validations;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="setups"></param>
    /// <param name="postConfigures"></param>
    public XiHanOptionsFactory(
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures)
        : this(setups, postConfigures, Array.Empty<IValidateOptions<TOptions>>())
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="setups"></param>
    /// <param name="postConfigures"></param>
    /// <param name="validations"></param>
    public XiHanOptionsFactory(
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
    {
        _setups = setups as IConfigureOptions<TOptions>[] ?? new List<IConfigureOptions<TOptions>>(setups).ToArray();
        _postConfigures = postConfigures as IPostConfigureOptions<TOptions>[] ?? new List<IPostConfigureOptions<TOptions>>(postConfigures).ToArray();
        _validations = validations as IValidateOptions<TOptions>[] ?? new List<IValidateOptions<TOptions>>(validations).ToArray();
    }

    /// <summary>
    /// 创建选项
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public virtual TOptions Create(string name)
    {
        var options = CreateInstance(name);

        ConfigureOptions(name, options);
        PostConfigureOptions(name, options);
        ValidateOptions(name, options);

        return options;
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    /// <param name="name"></param>
    /// <param name="options"></param>
    protected virtual void ConfigureOptions(string name, TOptions options)
    {
        foreach (var setup in _setups)
        {
            if (setup is IConfigureNamedOptions<TOptions> namedSetup)
            {
                namedSetup.Configure(name, options);
            }
            else if (name == string.Empty)
            {
                setup.Configure(options);
            }
        }
    }

    /// <summary>
    /// 后置配置选项
    /// </summary>
    /// <param name="name"></param>
    /// <param name="options"></param>
    protected virtual void PostConfigureOptions(string name, TOptions options)
    {
        foreach (var post in _postConfigures)
        {
            post.PostConfigure(name, options);
        }
    }

    /// <summary>
    /// 验证选项
    /// </summary>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <exception cref="OptionsValidationException"></exception>
    protected virtual void ValidateOptions(string name, TOptions options)
    {
        if (_validations.Length <= 0)
        {
            return;
        }

        List<string> failures = [];
        foreach (var validate in _validations)
        {
            var result = validate.Validate(name, options);
            if (result.Failed)
            {
                failures.AddRange(result.Failures);
            }
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(name, typeof(TOptions), failures);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected virtual TOptions CreateInstance(string name)
    {
        return Activator.CreateInstance<TOptions>();
    }
}
