Windows 10/11 游戏分辨率自动切换工具：进游戏切到 4:3 高刷，退出后自动恢复原来的桌面分辨率、刷新率、缩放和布局。

## 下载

- [安装版](https://github.com/794841182/DeltaResAuto/releases/download/v1.2.0/DeltaResAuto_v1.2.0_setup.exe) — 默认装到 `%LOCALAPPDATA%\Programs\DeltaResAuto`，不需要管理员权限
- [便携版](https://github.com/794841182/DeltaResAuto/releases/download/v1.2.0/DeltaResAuto_v1.2.0_portable.exe) — 免安装
- [SHA256SUMS.txt](https://github.com/794841182/DeltaResAuto/releases/download/v1.2.0/SHA256SUMS.txt) — 发布包校验值

## 校验

```text
14f8c889df4ac8785e5a0eda65159e78154ca8d5bd2c236fcfa83713ea511c39 *DeltaResAuto_v1.2.0_setup.exe
8a286f0542c5f076dd6da696f8e869d6b5bb8e2eb304972d14c0f8476c2f0627 *DeltaResAuto_v1.2.0_portable.exe
```

## 本次更新

- 新增珠链 / 深空 / 米白三套界面主题，右上角直接切换
- 米白主题单独适配卡片、输入框、胶囊、开关、菜单和分隔线
- 新增“刷新率”选项：跟随系统或该分辨率下驱动支持的任意 Hz
- 修复游戏运行中首次切换失败后不再重试的问题
- 修复游戏退出后恢复失败不会继续重试的问题
- 修复从未切换过时退出程序可能误改桌面分辨率的问题
- 卸载器改为只删除安装清单内的文件，保留同目录其他文件
- 配置增加滚动备份与损坏自愈，安装升级时自动备份旧配置
- 软件右下角与托盘菜单的反馈入口改为 GitHub Issues

## 系统要求

- Windows 10/11 x64
- .NET Framework 4.x（系统自带，不需要额外安装）
- 普通用户权限即可安装和运行

## 说明

安装包未做代码签名，首次运行可能触发 Windows SmartScreen；可以先核对 `SHA256SUMS.txt`，或用源码自行构建。

本项目为非官方工具，与游戏开发商、发行商无关联；不修改游戏文件，只调用 Windows 显示设置接口。
