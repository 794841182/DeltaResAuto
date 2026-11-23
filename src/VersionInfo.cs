using System.Reflection;
using System.Runtime.InteropServices;

#if INSTALLER
[assembly: AssemblyTitle("自动检测分辨率 安装程序")]
[assembly: AssemblyDescription("自动检测分辨率 v1.3.0 安装程序")]
#elif UNINSTALLER
[assembly: AssemblyTitle("自动检测分辨率 卸载程序")]
[assembly: AssemblyDescription("自动检测分辨率 v1.3.0 卸载程序")]
#else
[assembly: AssemblyTitle("自动检测分辨率")]
[assembly: AssemblyDescription("游戏分辨率自动切换工具")]
#endif

[assembly: AssemblyProduct("自动检测分辨率")]
[assembly: AssemblyCompany("冷渡-Van1sh")]
[assembly: AssemblyCopyright("Copyright (c) 2026 冷渡-Van1sh")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3.0")]
[assembly: ComVisible(false)]
