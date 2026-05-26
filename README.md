# Pacmine

[中文](README-zh.md)

Revolutionary assets package manager for Minecraft. Inspired by [pacman](https://pacman.archlinux.page/) from Arch Linux.

## Why Pacmine?

- **Universal assets management.** Packages provide not only mods, but also resource packs, config flies, shader packs, or literally any files in your game directory.
- **Do what a package manager does.** Supports dependencies and conflicts check. Package and file records registered in local database make upgrading and uninstalling an ease.
- **Create packages in a snap.** You can write all the build information of a package into a single Lua script (namely PackageCraft) and Pacmine will build the package automatically for you, just like how [PKGBUILD](https://wiki.archlinux.org/title/PKGBUILD) works.
- **High priority on security.** Any overwriting or deleting operation on files are handled with great caution. Build scripts are executed in a pure-C#-implemented and restricted-by-default Lua state environment with standard library totally disabled.
- **Community-friendly distribution mode.** You do not have to acquire the source code of a mod or distribute mod jar file to maintain a Pacmine package - simply share the Lua script and let everyone build their own packages.

## Architecture

Pacmine as a C# library is modular by design. You can include only the features you want without the heavy dependacies.

| Package Name           | Description                                                                                                               | Depends On                                                                                     |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| Pacmine.Core           | Base library of core models and concepts.                                                                                 | [semver][semver]                                                                               |
| Pacmine.PackageCraft   | Utilities to build Pacmine packages.                                                                                      | Pacmine.Core, [Downloader][downloader], [LuaCSharp][luacsharp]                                 |
| Pacmine.Environment    | Utilities to manage package registry in game instances, also provides file operations to install packages on filesystems. | Pacmine.Core                                                                                   |
| Pacmine.Database (WIP) | Utilities to manage local package database.                                                                               | Pacmine.Core                                                                                   |
| Pacmine.Console        | A CLI application to download, install, manage and build Pacmine packages.                                                | All `Pacmine` prefixed libraries, [System.CommandLine][system.commandline], [Wcwidth][wcwidth] |

All the modules share the same version number. Every new release bumps the version of all modules, even if one had not received any update since the last release.

## Documentation

Check online documentation at <https://pacmine.robomico.cn> or build the docs site locally:

```bash
# Install DocFX global tool (if you haven't)
dotnet tool install -g docfx
# Build the documentation and start a local server
docfx docfx/docfx.json --serve
# Now go to http://localhost:8080 in your browser
```

## Integrating Guides

soon

## 3rd Party Ports

soon

[downloader]: https://github.com/bezzad/Downloader
[luacsharp]: https://github.com/nuskey8/Lua-CSharp
[semver]: https://github.com/WalkerCodeRanger/semver
[system.commandline]: https://www.nuget.org/packages/System.CommandLine
[wcwidth]: https://github.com/spectreconsole/wcwidth
