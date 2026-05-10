# Pacmine

为 Minecraft 设计的新一代资产包管理器，灵感来源于 Arch Linux 的 [pacman](https://pacman.archlinux.page/)。

## 为什么选择 Pacmine？

- **通用资产管理**：不仅支持模组，还支持资源包、光影包、配置文件、整合包，甚至游戏目录下的任何文件。
- **真正的包管理功能**：支持依赖关系与冲突检测。自带本地数据库记录包和文件信息，使升级和卸载变得轻而易举。
- **一键构建包**：你可以将包的所有构建信息写入一个 Lua 脚本（即 PackageCraft），Pacmine 会自动为你构建包，就像 Arch Linux 的 [PKGBUILD](https://wiki.archlinux.org/title/PKGBUILD) 一样。
- **高度重视安全**：任何文件的覆盖或删除操作都经过极其谨慎的处理。构建脚本在纯 C# 实现且默认受限的 Lua 环境中执行，标准库完全禁用。
- **社区友好的分发模式**：你无需获取模组源码或分发模组 `.jar` 文件来维护 Pacmine 包——只需分享 Lua 脚本，让每个人都能构建自己的包。

## 架构

Pacmine 作为一个 C# 库，采用模块化设计。你可以只包含你需要的功能，而无需引入繁重的依赖。

| 包名                   | 描述                                                                             | 依赖项                                                          |
| ---------------------- | -------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| Pacmine.Core           | 核心模型与概念的基础库。                                                         | 无                                                              |
| Pacmine.PackageCraft   | 用于构建 Pacmine 包的工具。                                                      | Pacmine.Core, [Downloader][downloader], [LuaCSharp][luacsharp]  |
| Pacmine.Environment    | 用于管理游戏实例中组建包注册表的工具，同时提供在文件系统上安装包的文件操作功能。 | Pacmine.Core                                                    |
| Pacmine.Database (WIP) | 用于管理本地包数据库的工具。                                                     | Pacmine.Core                                                    |
| Pacmine.Console        | CLI 应用程序，用于下载、安装、管理和构建 Pacmine 包。                            | 所有 `Pacmine` 前缀库, [System.CommandLine][system.commandline] |

所有模块共享相同的版本号。每次新发布都会更新所有模块的版本，即使某个模块自上次发布以来未收到任何更新。

## 文档

soon

## 集成指南

soon

## 第三方移植

soon

[downloader]: https://github.com/bezzad/Downloader
[luacsharp]: https://github.com/nuskey8/Lua-CSharp
[system.commandline]: https://www.nuget.org/packages/System.CommandLine
