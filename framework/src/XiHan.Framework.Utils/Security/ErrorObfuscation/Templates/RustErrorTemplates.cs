#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RustErrorTemplates
// Guid:0b1c2d3e-5f6a-7b8c-9d0e-1f2a3b4c5d6e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// Rust 错误模板
/// </summary>
internal static class RustErrorTemplates
{
    /// <summary>
    /// Rust 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "thread 'main' panicked at 'called `Option::unwrap()` on a `None` value', src/handlers/user.rs:45:23\nstack backtrace:\n   0: rust_begin_unwind\n             at /rustc/d5a82bbd26e1ad8b7401f6a718a9c57c96905483/library/std/src/panicking.rs:575:5\n   1: core::panicking::panic_fmt\n             at /rustc/d5a82bbd26e1ad8b7401f6a718a9c57c96905483/library/core/src/panicking.rs:64:14\n   2: core::panicking::panic\n             at /rustc/d5a82bbd26e1ad8b7401f6a718a9c57c96905483/library/core/src/panicking.rs:114:5\n   3: myapp::handlers::user::get_user\n             at ./src/handlers/user.rs:45:23",
        "thread 'main' panicked at 'index out of bounds: the len is 3 but the index is 5', src/services/processor.rs:89:17\nstack backtrace:\n   0: rust_begin_unwind\n   1: core::panicking::panic_fmt\n   2: core::panicking::panic_bounds_check\n   3: myapp::services::processor::process_data\n             at ./src/services/processor.rs:89:17",
        "Error: Connection refused (os error 111)\n   0: myapp::database::connect\n             at src/database.rs:34:5\n   1: myapp::main\n             at src/main.rs:23:5\n   2: core::ops::function::FnOnce::call_once\n             at /rustc/d5a82bbd26e1ad8b7401f6a718a9c57c96905483/library/core/src/ops/function.rs:250:5",
        "thread 'main' panicked at 'called `Result::unwrap()` on an `Err` value: ParseIntError { kind: InvalidDigit }', src/parsers/input.rs:67:38\nstack backtrace:\n   0: rust_begin_unwind\n   1: core::panicking::panic_fmt\n   2: core::result::unwrap_failed\n   3: myapp::parsers::input::parse_user_id\n             at ./src/parsers/input.rs:67:38",
        "Error: No such file or directory (os error 2)\n   0: std::fs::read_to_string\n             at /rustc/d5a82bbd26e1ad8b7401f6a718a9c57c96905483/library/std/src/fs.rs:394:5\n   1: myapp::config::load_config\n             at src/config.rs:28:20\n   2: myapp::main\n             at src/main.rs:15:5",
        "thread 'tokio-runtime-worker' panicked at 'attempt to divide by zero', src/calculations/math.rs:156:9\nstack backtrace:\n   0: rust_begin_unwind\n   1: core::panicking::panic\n   2: myapp::calculations::math::divide\n             at ./src/calculations/math.rs:156:9"
    ];
}
