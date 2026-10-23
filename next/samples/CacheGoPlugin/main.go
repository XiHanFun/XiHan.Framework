// CacheGoPlugin 是用 framework-sdk-go 写的 caching.distributed 远程 Provider 插件，由 XiHan.Framework 的进程外插件宿主加载、
// 经 Caching.Remote 适配器成为分布式缓存 Provider（P4-07）：操作集的实现来自 SDK 的 examples/cache，这里只提供可执行入口。
package main

import (
	"context"
	"fmt"
	"os"

	"github.com/xihanfun/framework-sdk-go/examples/cache"
)

func main() {
	if err := cache.Serve(context.Background()); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}
