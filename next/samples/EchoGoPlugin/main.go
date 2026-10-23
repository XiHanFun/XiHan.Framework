// EchoGoPlugin 是用 framework-sdk-go 写的回声插件，由 XiHan.Framework 的进程外插件宿主加载（P4-06）：
// 清单与操作来自 SDK 的 examples/echo，这里只提供可执行入口；构建产物目录里要放同一份 xihan.manifest.json。
package main

import (
	"context"
	"fmt"
	"os"

	"github.com/xihanfun/framework-sdk-go/examples/echo"
)

func main() {
	if err := echo.Serve(context.Background()); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}
