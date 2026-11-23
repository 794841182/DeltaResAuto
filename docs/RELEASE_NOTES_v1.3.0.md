Windows 10/11 游戏分辨率自动切换工具：面向原生 4K/2K 及以上屏幕，支持全屏无边框下用 1K 拉满帧率，也照顾习惯 4:3 的 FPS 玩家；退出游戏默认恢复屏幕最高分辨率。

## 下载

- [安装版](https://github.com/Van1sh-LDu/DeltaResAuto/releases/download/v1.3.0/DeltaResAuto_v1.3.0_setup.exe) — 默认装到 `%LOCALAPPDATA%\Programs\DeltaResAuto`，不需要管理员权限
- [便携版](https://github.com/Van1sh-LDu/DeltaResAuto/releases/download/v1.3.0/DeltaResAuto_v1.3.0_portable.exe) — 免安装
- [SHA256SUMS.txt](https://github.com/Van1sh-LDu/DeltaResAuto/releases/download/v1.3.0/SHA256SUMS.txt) — 发布包校验值

## 校验

```text
8d56f2f7513b2918beec064be18b4db66ff344ca28f0e7c88f6e25fc7b457a6c *DeltaResAuto_v1.3.0_setup.exe
089408bb01c011d8c5e8902d9701989e3ad10b6b31f8b68be89ca4a3f4182e6c *DeltaResAuto_v1.3.0_portable.exe
```

## 本次更新

- 新增“画面比例”选项：游戏运行时、退出游戏后分别选择 16:9 / 16:10 / 4:3 等比例
- 选择比例后自动匹配当前显示器支持的分辨率；游戏模式优先 1K 附近，退出模式优先该比例下最高分辨率
- 比例列表来自驱动真实枚举，不会出现系统不支持的模式
- 修复“立即恢复桌面分辨率”在没有切换记录时点击无响应：现在会直接应用你设置的退出分辨率
- 保留刷新率、默认最高分辨率恢复、三套主题、配置备份和安装校验

## 系统要求

- Windows 10/11 x64
- .NET Framework 4.x（系统自带，不需要额外安装）
- 普通用户权限即可安装和运行

## 说明

安装包未做代码签名，首次运行可能触发 Windows SmartScreen；可以先核对 `SHA256SUMS.txt`，或用源码自行构建。

本项目为非官方工具，与游戏开发商、发行商无关联；不修改游戏文件，只调用 Windows 显示设置接口。
