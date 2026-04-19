# Pacmine

Revolutionary assets package manager for Minecraft. Inspired by [pacman](https://pacman.archlinux.page/) from Arch Linux.

## Why Pacmine?

- **Universal assets management.** Packages provide not only mods, but also resource packs, config flies, shader packs, or literally any files in your game directory.
- **Do what a package manager does.** Supports dependencies and conflicts check. Package and file records registered in local database make upgrading and uninstalling an ease.
- **Create packages in a snap.** You can write all the build information of a package into a single Lua script (namely PackageCraft) and Pacmine will build the package automatically for you, just like how [PKGBUILD](https://wiki.archlinux.org/title/PKGBUILD) works.
- **Community-friendly distribution mode.** You do not have to acquire the source code of a mod or distribute mod jar file to maintain a Pacmine package - simply share the Lua script and let everyone build their own packages.

## Acknowledgement

- [Arch Linux](https://archlinux.org/) and pacman: inspiration of the design
- Dependencies NuGet packages of the project:
  - [Lua-CSharp](https://github.com/nuskey8/Lua-CSharp)
  - [Downloader](https://github.com/bezzad/Downloader)
