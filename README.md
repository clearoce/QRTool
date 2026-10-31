# QRTool

Windows 桌面小工具：输入文本 → 本地预览二维码 → 以 **TSPL 原始指令**发送到指定的 Windows 打印队列（佳博 **GP-9134T**，300 DPI，热转印/热敏标签机）。

只打印二维码，不打印文字；标签宽、高、间距、DPI、ECC、单元宽度、份数均可配置并保存。

## 为什么用 C# WinForms

这个工具功能不复杂，之前用 PySide6/Qt 时 exe 本体约 55 MB，绝大部分是 Qt 运行时。现在改成：

- **C# WinForms + .NET Framework 4.8**：Windows 10/11 自带运行时，目标机不需要装 Python、.NET SDK 或额外依赖。
- **单文件 exe，约 249 KB**：QRCoder.dll 以资源方式内嵌，最终只交付一个 exe。
- **启动快**：不是 PyInstaller onefile，不需要每次解压到临时目录。

## 环境要求

- Windows 10/11 x64（自带 .NET Framework 4.8）
- GP-9134T 的 Windows 驱动已安装，并创建了打印队列（USB / 串口 / 并口均可）
- 构建机只需要 Windows 自带的 `.NET Framework 4.x` 的 `csc.exe`；不需要 Visual Studio 或 .NET SDK

## 直接运行

```powershell
dist\QRTool.exe
```

- 双击即可，无安装过程、无控制台窗口。
- 配置保存在 `%APPDATA%\QRTool\config.json`，不保存输入文本。
- exe 未做代码签名，SmartScreen/Defender 可能提示；选择“仍要运行”即可。
- 这是窗口程序；命令行调试可用下面的 headless 入口。

## 界面功能

1. **文本内容**：支持中文、引号、多行；实时显示字节数/容量上限。
2. **打印机**：枚举 Windows 打印队列；「刷新」重新枚举；「测试」发送固定内容 `QRTool TEST` 的测试标签（强制 1 份）。
3. **标签与二维码**：标签宽/高/间距（mm）、DPI、份数、ECC、单元宽度（自动或 1–10）、编码（UTF-8/GB18030）、Model 2、保留换行、兼容模式。
4. **生成预览**：用 QRCoder 生成本地预览，显示二维码物理尺寸、模块数和 dots/模块。
5. **打印**：后台线程通过 `winspool.drv` 的 RAW 接口提交，成功后状态栏显示作业号。
6. **导出 TSPL**：保存原始 `.prn` 字节，便于无设备时检查或在目标机器上直接发送。
7. **恢复默认**：恢复标签/二维码设置，保留当前打印机和上次导出目录。

快捷键：`Ctrl+P` 打印，`Ctrl+Enter` 生成预览。

打印/导出前如果存在风险提示（模块过小、标签超过 80 mm、兼容模式、长内容等），会先弹出确认框。

## TSPL 输出

以 40×30 mm、300 DPI、ECC M、内容 `hello` 为例：

```
SIZE 40 mm,30 mm
GAP 2 mm,0 mm
DIRECTION 1
REFERENCE 0,0
CLS
QRCODE 131,72,M,10,A,0,M2,S7,"hello"
PRINT 1,1
```

每行以 `\r\n` 结束，最后一行也有 `\r\n`。

### 排版规则

- 300 DPI：`1 mm ≈ 11.8 dots`，TSPL 只取整数部分。
- 标签宽 20–98 mm；GP-9134T 打印头宽度 80 mm，标签更宽时二维码按 80 mm 可打印区域居中，并给出警告。
- 二维码默认留 **4 个 module 静区**，外层再加 **2 mm 安全边距**。
- 单元宽度自动选择 1–10 dots 中能放下的最大值；小于 4 dots（约 0.34 mm）会警告。
- **版本按字节模式的最坏情况选**：C# 侧内置每个 QR 版本/ECC 的最大字节容量表，布局不会因为打印机固件没有做数字/字母优化而被撑大。
- 内容中的双引号按 TSPL 手册写成 `\"`；内容内换行保留为 CRLF（可关闭）；末尾反斜杠补一个空格，避免转义掉结束引号。
- 默认 `M2,S7`；「兼容模式」或不勾选 Model 2 时省略 Model/Mask。
- 标签范围：宽 20–98 mm、高 5–300 mm、间距 0–25.4 mm、份数 1–999。

## 配置

路径：`%APPDATA%\QRTool\config.json`

```json
{
  "version": 1,
  "printer_name": "GP-9134T",
  "label_width_mm": 40.0,
  "label_height_mm": 30.0,
  "gap_mm": 2.0,
  "dpi": 300,
  "ecc": "M",
  "cell_width": 0,
  "copies": 1,
  "encoding": "utf-8",
  "use_model2": true,
  "keep_newlines": true,
  "compat_mode": false,
  "last_export_dir": ""
}
```

- 原子写入（临时文件 + `File.Replace`）。
- 启动时逐字段校验；无法解析的配置会重命名为 `config.json.bad`，不会静默覆盖。
- 不保存输入文本。

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

`build.ps1` 使用 Windows 自带的：

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

并把 `lib\QRCoder.dll` 作为资源嵌入，生成 `dist\QRTool.exe`。

## 无设备验证

当前开发机没有 GP-9134T，真实出纸/对齐/扫码尚未验证；已完成：

```powershell
dist\QRTool.exe export hello scratch\hello.prn
dist\QRTool.exe preview hello scratch\hello.png
dist\QRTool.exe print hello "GP-9134T"
```

- `export` 生成的 `hello` 为 **115 字节**，与上面的模板逐字节一致。
- `preview` 生成的 PNG 用 `zxing-cpp` 解码：`hello` 解出 `hello`，`你好` 解出 `你好`。
- GUI 已启动并截图检查布局；打印机枚举到 `Microsoft Print to PDF`。
- `print` 对不存在的队列返回退出码 1，并在 `%APPDATA%\QRTool\qrtool.log` 写入 `Win32Exception`（`OpenPrinter` 错误路径）。
- exe 是 `--windowed` 程序，headless 命令不输出到控制台；用退出码和生成的文件判断结果。错误日志在 `%APPDATA%\QRTool\qrtool.log`。

## 现场调试清单（有 GP-9134T 时）

1. 安装佳博 GP-9134T 驱动，确认 Windows 队列名称。
2. 打开 QRTool，选择该队列；点「测试」，确认能出纸并扫码。
3. 打印中文、引号、多行内容，手机扫码核对内容。
4. 检查标签偏移/多走纸：校准间隙传感器，确认 `GAP` 与标签纸实际间距一致。
5. 若标签被裁切：确认标签宽/高；GP-9134T 可打印宽度为 80 mm；可减小单元宽度或增大标签。
6. 若固件不接受 `M2,S7`：勾选「兼容模式」再试。
7. 若中文扫码乱码：编码改为 GB18030；先用纯 ASCII 排除传输问题。
8. 若完全空白：检查碳带/热敏面、打印机是否处于 TSPL 模式、驱动端口是否正确。
9. 需要区分应用/驱动问题时，可把 `.prn` 直接发给队列（队列已共享时）：

   ```powershell
   cmd /c copy /b label.prn "\\localhost\GP-9134T"
   ```

   或使用厂商的 Raw Print / 诊断工具发送同一文件。

## 项目结构

```
src/
  Program.cs               # 入口、DPI、headless export/preview/print、QRCoder 资源解析
  MainForm.cs              # WinForms 界面
  AppConfig.cs             # 配置加载/保存/校验
  Tspl.cs                  # TSPL 生成、容量表、布局
  QrPreview.cs             # QRCoder 本地预览
  PrinterService.cs        # winspool RAW 打印
lib/
  QRCoder.dll              # net40 版本，构建时嵌入 exe
  QRCoder.LICENSE.txt      # MIT 许可
assets/
  qrtool.ico               # exe/窗口图标
  qrtool.png               # 图标源图
local/scripts/make_icon.py # 可选：重新生成图标（需要 Python + qrcode/Pillow）
build.ps1                  # csc 构建脚本
dist/QRTool.exe            # 本地构建产物（.gitignore，不入库）
```

## 已知限制

- 需要 Windows 10/11 自带的 .NET Framework 4.8；更老的 Windows 需要单独安装。
- 仅支持 x64 Windows；仅支持 Windows 打印队列，不做 IP:9100 直连。
- 一张标签只放一个二维码，不做批量排版；按需求只打印二维码，不打印人眼可读文字。
- 本地预览使用与 TSPL 相同的 payload 和保守版本；最终 QR 的 model/mask/优化模式由打印机固件决定。
- exe 未签名；headless 模式没有控制台输出。
- 没有真实 GP-9134T，实际出纸、对齐和扫码仍需现场验证。

## 第三方

- [QRCoder](https://github.com/codebude/QRCoder)（MIT）用于本地二维码预览；许可文本见 `lib/QRCoder.LICENSE.txt`。
- TSPL 指令参考：[TSPL/TSPL2 Programming Manual](https://qrprt.com/tv/TSPL.pdf)。
- 打印机规格：[佳博 GP-9134T/9135T](https://www.gprinter.net/rzytm/493.html)。
