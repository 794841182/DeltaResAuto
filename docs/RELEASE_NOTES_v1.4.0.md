Windows 10/11 游戏分辨率自动切换工具，现已更名为 **ResPilot**：面向原生 4K/2K 及以上屏幕，支持全屏无边框下用 1K 拉满帧率，也照顾习惯 4:3 的 FPS 玩家；退出游戏默认恢复屏幕最高分辨率。

## 下载

- [安装版](https://github.com/Van1sh-LDu/ResPilot/releases/download/v1.4.0/ResPilot_v1.4.0_setup.exe) — 默认装到 `%LOCALAPPDATA%\Programs\ResPilot`，不需要管理员权限
- [便携版](https://github.com/Van1sh-LDu/ResPilot/releases/download/v1.4.0/ResPilot_v1.4.0_portable.exe) — 免安装
- [SHA256SUMS.txt](https://github.com/Van1sh-LDu/ResPilot/releases/download/v1.4.0/SHA256SUMS.txt) — 发布包校验值

## 校验

```text
7c11eda55e73392f227180ecfd9d5e80177ca0ed3735cc11c2da5a70110c2729 *ResPilot_v1.4.0_setup.exe
789bd9c41d520f4e5531179dea02c64d672a0d2136cca95dcb8b226ddc882adf *ResPilot_v1.4.0_portable.exe
```

## 本次更新

- 正式更名为 ResPilot：窗口标题、程序文件、安装包、卸载项和开机启动项统一为英文名
- 安装目录改为 `%LOCALAPPDATA%\Programs\ResPilot`，数据目录改为 `%LOCALAPPDATA%\ResPilot`
- 首次启动自动迁移原配置；安装与卸载会清理旧进程、旧开机启动项和旧卸载项
- 功能与 1.3.0 一致：游戏运行时 / 退出游戏后分别设置画面比例，自动匹配分辨率，支持刷新率与立即恢复

## 系统要求

- Windows 10/11 x64
- .NET Framework 4.x（系统自带，不需要额外安装）
- 普通用户权限即可安装和运行

## 说明

安装包未做代码签名，首次运行可能触发 Windows SmartScreen；可以先核对 `SHA256SUMS.txt`，或用源码自行构建。

本项目为非官方工具，与游戏开发商、发行商无关联；不修改游戏文件，只调用 Windows 显示设置接口。
