#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RubyErrorTemplates
// Guid:9a0b1c2d-4e5f-6a7b-8c9d-0e1f2a3b4c5d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// Ruby 错误模板
/// </summary>
internal static class RubyErrorTemplates
{
    /// <summary>
    /// Ruby 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "NoMethodError: undefined method `name' for nil:NilClass\n\tfrom /app/controllers/users_controller.rb:45:in `show'\n\tfrom /usr/local/bundle/gems/actionpack-7.0.4/lib/action_controller/metal/basic_implicit_render.rb:6:in `send_action'\n\tfrom /usr/local/bundle/gems/actionpack-7.0.4/lib/abstract_controller/base.rb:215:in `process_action'",
        "ActiveRecord::ConnectionNotEstablished: No connection pool with 'primary' found.\n\tfrom /usr/local/bundle/gems/activerecord-7.0.4/lib/active_record/connection_adapters/abstract/connection_pool.rb:67:in `retrieve_connection'\n\tfrom /app/models/user.rb:23:in `find_by_email'\n\tfrom /app/controllers/sessions_controller.rb:12:in `create'",
        "ArgumentError: wrong number of arguments (given 2, expected 1)\n\tfrom /app/services/calculator.rb:34:in `divide'\n\tfrom /app/controllers/math_controller.rb:56:in `calculate'\n\tfrom /usr/local/bundle/gems/actionpack-7.0.4/lib/action_controller/metal/basic_implicit_render.rb:6:in `send_action'",
        "Errno::ENOENT: No such file or directory @ rb_sysopen - /app/config/database.yml\n\tfrom /app/config/initializers/database.rb:8:in `initialize'\n\tfrom /app/config/application.rb:23:in `<class:Application>'\n\tfrom /app/config/application.rb:10:in `<module:MyApp>'",
        "SyntaxError: /app/models/product.rb:67: syntax error, unexpected end-of-input, expecting end\n\tfrom /usr/local/bundle/gems/bootsnap-1.16.0/lib/bootsnap/load_path_cache/core_ext/kernel_require.rb:32:in `require'\n\tfrom /app/config/environment.rb:5:in `<top (required)>'",
        "TypeError: no implicit conversion of nil into String\n\tfrom /app/helpers/view_helper.rb:89:in `+'\n\tfrom /app/views/users/show.html.erb:12:in `_app_views_users_show_html_erb__123456789_98765'\n\tfrom /usr/local/bundle/gems/actionview-7.0.4/lib/action_view/template.rb:157:in `block in render'",
        "RuntimeError: Stack level too deep\n\tfrom /app/models/category.rb:45:in `ancestors'\n\tfrom /app/models/category.rb:45:in `ancestors'\n\tfrom /app/models/category.rb:45:in `ancestors'",
        "JSON::ParserError: 765: unexpected token at '<!DOCTYPE html>'\n\tfrom /usr/local/lib/ruby/3.2.0/json/common.rb:216:in `parse'\n\tfrom /app/services/api_client.rb:78:in `fetch_data'\n\tfrom /app/controllers/api_controller.rb:34:in `index'"
    ];
}
