using System.Reflection;
using System.Runtime.InteropServices;

#if INSTALLER
[assembly: AssemblyTitle("ResPilot 安装程序")]
[assembly: AssemblyDescription("ResPilot v1.4.0 安装程序")]
#elif UNINSTALLER
[assembly: AssemblyTitle("ResPilot 卸载程序")]
[assembly: AssemblyDescription("ResPilot v1.4.0 卸载程序")]
#else
[assembly: AssemblyTitle("ResPilot")]
[assembly: AssemblyDescription("游戏分辨率自动切换工具")]
#endif

[assembly: AssemblyProduct("ResPilot")]
[assembly: AssemblyCompany("冷渡-Van1sh")]
[assembly: AssemblyCopyright("Copyright (c) 2026 冷渡-Van1sh")]
[assembly: AssemblyVersion("1.4.0.0")]
[assembly: AssemblyFileVersion("1.4.0.0")]
[assembly: AssemblyInformationalVersion("1.4.0")]
[assembly: ComVisible(false)]
