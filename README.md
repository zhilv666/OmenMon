# OmenMon · WPF 风扇控制版

> 本项目是 **[OmenMon](https://github.com/OmenMon/OmenMon)** 的修改版（fork），在原有硬件监控 / 控制能力之上，新增了一套 WPF 图形界面与基于温度的风扇曲线控制，主要面向 **HP Victus / Omen** 笔记本。
>
> 原始项目主页与文档：<https://omenmon.github.io/>

---

## 简介

**OmenMon** 是一款轻量级工具，通过 HP 笔记本的嵌入式控制器（EC）与 WMI BIOS 接口读取硬件状态、调节风扇转速，用以替代 _Omen Hub / Omen Control Center_ 的实用功能，且不联网、无广告、无捆绑。

本分支在此基础上重写并扩展了图形界面，使其在 Victus / Omen 机型上更易用。

## 本分支新增 / 修改的内容

相较上游 OmenMon，本仓库的改动主要集中在 `App/Wpf`：

- **全新 WPF 控制面板**：暗色主题、侧边栏导航（监控面板 / 风扇控制 / 性能模式 / 曲线设置 / 悬浮监控 / 设置）。
- **基于温度的风扇曲线**：可视化编辑「温度 → 转速」表，自动模式下按曲线动态调速。
- **悬浮监控（Overlay）**：桌面常驻、始终置顶的小面板，实时显示 CPU / GPU 温度与风扇转速。
  - 提供两种模板样式：**经典卡片**（环形仪表）与 **简约信息条**（横向信息条 + 进度条）。
  - 支持在界面上实时调节**透明度**与**大小比例**。
- **性能模式关闭风扇**：新增带温度保护的临时停扇档，性能页与托盘同步提供入口。
- **托盘图标**：快速显示面板、切换悬浮监控、应用风扇预设。
- 精简了上游的旧版 WinForms 界面代码。

> ⚠️ 由于需要访问 EC / BIOS，程序必须**以管理员身份运行**。

## 关闭风扇（温度保护）

“安静”仍是 36% 固定转速；需要临时停扇时，在**性能模式 → 关闭风扇**或托盘的同名菜单选择。

- 只有当次 CPU、GPU 温度都有效且在 **10°C 至低于 60°C** 范围内时才发送专用停扇指令。10°C 是临时停扇的保守安全下限，不是硬件的物理温度极限。
- 发出停扇指令后立即复读温度，随后按 1 秒间隔检查；任一温度达到 **60°C**、温度低于可信下限、温度/控制状态读取异常或固件取消停扇，都会取消本次停扇并请求 BIOS 自动散热。恢复未确认时会提示具体失败步骤并继续重试恢复。
- 无效或读取失败的 CPU/GPU 温度显示为 `--`，不会继续展示旧缓存；另一侧正常的读数独立刷新。异常提示保留源名称与观测值用于诊断，不会改用 GPU 或任意其他探头冒充 CPU。
- 针对停扇时 RTMP 变为 `2°C` 的固件行为，支持的单 CPU Intel Alder Lake Core（型号 `0x97` / `0x9A`，包括 i5-12450H）在停扇前、停扇期间和恢复待确认期间改用 **CPU Package (MSR)** 独立封装温度，GPU 仍使用配置的传感器。成功退出停扇后，普通监控恢复原有 CPU 温度来源；无需修改 XML 配置。
- 独立测温复用已有驱动的只读 MSR 接口，不新增驱动、不写入 MSR。读取时验证 TjMax、有效位及温度范围；独立源一旦读取失败，不回退到 RTMP 或旧缓存，而是拒绝/撤销停扇。不支持的 CPU 保留原来源与异常保护，不假定所有型号均已兼容。成功停扇的状态信息会显示实际 CPU 温度来源。
- CPU 封装温度的寄存器换算参考 [Linux coretemp](https://github.com/torvalds/linux/blob/master/drivers/hwmon/coretemp.c) 与 [LibreHardwareMonitor IntelCpu](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitorLib/Hardware/Cpu/IntelCpu.cs)。模拟测试不代表本机 MSR 接口已获系统允许，仍需以管理员运行验证。
- 点击后若显示“停扇前准备失败，未发送停扇指令”，表示尚未执行停扇。可选择其他固定档、手动或曲线：程序确认停扇开关已释放，并严格检查目标档位的首次写入后才结束恢复重试，不要求 BIOS 自动档命令先成功。若目标设置也失败，仍保留恢复保护；非零 RPM 本身不作为恢复成功的依据。
- 对于 BIOS 等级指令返回错误、但实际目标已生效的机型，会新鲜读取两个风扇的 EC 目标等级（SRP1 / SRP2）；只有两者都与请求值一致，才允许接管并结束恢复重试。自动等级也必须准确读回 `FF FF`，且完成其余恢复步骤。不会用缓存或实际转速代替确认，也不会自动改用 EC 写入。无法确认时，提示会包含 BIOS 原始错误及每个风扇的目标 / 读回值（十六进制）。
- 保护触发后不会因降温自动再次停扇。切换其他档位、正常退出或休眠时解除停扇；唤醒、重新启动不恢复此选择。正常退出前若多次恢复仍未确认，会取消退出并保留监控重试。
- “关闭指令已确认”不等于风扇立刻停转，请看实际 RPM。机型、固件可能拒绝停扇或自行重新启动风扇，软件不会反复强制关闭。

> ⚠️ 仅用于低负载临时降噪，不适用于游戏、渲染等高负载。传感器配置必须真实对应 CPU / GPU；缺少可靠温度时不可使用。软件保护不是固件级安全保证，进程被强制结束、系统崩溃或硬件通信故障时无法保证恢复；若提示恢复未确认，请立即检查实际风扇状态，必要时退出负载并关机。

## 构建与运行

需要 .NET Framework 4.8 与 Visual Studio 2022 Build Tools（或 Community）。

```bat
make.cmd build
```

构建产物位于 `Bin\OmenMon.exe`。运行时请使用管理员权限；若已有实例在运行，会占用 `Bin\` 目录，需先退出再重新构建。

不访问真实硬件的回归测试（在 VS Developer Command Prompt 中运行，无需管理员权限）：

```bat
msbuild Tests\OmenMon.Tests.csproj /t:Build /p:Configuration=Release /p:Platform=x64
Tests\Bin\OmenMon.Tests.exe
```

这些测试使用模拟 EC、风扇及温度传感器，不会加载驱动或修改本机风扇设置。

## 致谢

衷心感谢原始项目及其作者：

- **[OmenMon](https://github.com/OmenMon/OmenMon)** — 由 **[Piotr Szczepański](https://piotr.szczepanski.name/)** 开发。本项目的全部底层硬件访问（EC / BIOS / WMI）、命令行能力与整体架构均来自于此，没有它就没有本分支。
- 上游项目所依赖 / 致谢的其他工作，详见其 [acknowledgements](https://omenmon.github.io/more#acknowledgements)。

如果这个工具对你有帮助，也请为[上游仓库](https://github.com/OmenMon/OmenMon)点一个 Star。

## 许可协议

**OmenMon** Copyright © 2023-2024 [Piotr Szczepański](https://piotr.szczepanski.name/)

本项目遵循 **[GNU 通用公共许可证第 3 版（GPL-3.0）](https://www.gnu.org/licenses/gpl-3.0.html)**，与上游保持一致。完整协议文本见仓库中的 [`LICENSE.md`](LICENSE.md)。本分支对源代码的修改同样以 GPL-3.0 授权发布。

### 合规说明

本分支是 OmenMon 的修改版，为满足 GPL-3.0 的要求，本仓库：

- 保留了原始的 `LICENSE.md` 与源码文件中的版权声明；
- 以显著方式说明「本项目为 OmenMon 的修改版」并列出主要改动（见上文）；
- 全部修改仍以 GPL-3.0 授权，并在分发二进制时提供对应完整源代码。

在满足以上条件的前提下，对本软件的**修改与自用**完全符合 GPL-3.0；只要在**对外分发**时同样保持源码开放且沿用 GPL-3.0，就不构成违规。

> 本软件与 HP 无任何关联，也未获其背书。文中出现的品牌名称仅用于说明用途。

## 相关项目

- [XML Translator](https://github.com/Initsnow/xmltranslator) by **[@Initsnow](https://github.com/Initsnow)** — 用于[翻译 OmenMon](https://github.com/OmenMon/Localization)
- [HP Omen Sequencer Keyboard Lights Control Utility](https://github.com/slysherz/lights-for-omen-sequencer) by **[@slysherz](https://github.com/slysherz/)**
