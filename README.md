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
- **托盘图标**：快速显示面板、切换悬浮监控、应用风扇预设。
- 精简了上游的旧版 WinForms 界面代码。

> ⚠️ 由于需要访问 EC / BIOS，程序必须**以管理员身份运行**。

## 构建与运行

需要 .NET Framework 4.8 与 Visual Studio 2022 Build Tools（或 Community）。

```bat
make.cmd build
```

构建产物位于 `Bin\OmenMon.exe`。运行时请使用管理员权限；若已有实例在运行，会占用 `Bin\` 目录，需先退出再重新构建。

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
