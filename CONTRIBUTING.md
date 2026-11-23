# 参与 ResPilot

## 报告问题

先开 [Issue](../../issues)，尽量附上：

- Windows 版本（`winver`）
- 显卡型号与驱动版本
- 显示器分辨率、刷新率、缩放比例
- 游戏名称与进程名
- 复现步骤，以及切换前 / 切换后的分辨率
- `%LOCALAPPDATA%\ResPilot\auto.log` 或 `crash.log` 的相关片段

不要在公开 Issue 里贴完整配置、账号信息或带有个人路径的截图。

## 提交 PR

1. Fork 仓库，从 `main` 切一个分支。
2. 改完后在 Windows 上运行构建：

   ```powershell
   pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\build.ps1
   ```

3. 至少测试：安装 / 便携版启动、进游戏切换、退出恢复、手动“立即恢复桌面分辨率”、卸载。
4. 在 PR 描述里写清楚测试环境（Windows 版本、显卡、显示器、游戏）。

## 代码约定

- 主程序保持单文件 WinForms，不引入第三方 NuGet 包；需要加依赖先开 Issue 讨论。
- 分辨率切换只走 `ChangeDisplaySettingsEx`，不要改成注册表硬写或模拟点击。
- 界面文案保持现有中文风格；新增设置项要同步 `settings.example.ini` 和 README。
- P/Invoke 结构体字段顺序不要随意调整，改之前先核对 Windows SDK 定义。

## License

提交代码即表示同意以本仓库的 MIT 许可证发布。第三方字体、图标、背景图不在此授权范围内，见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
