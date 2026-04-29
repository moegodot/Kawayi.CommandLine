# 仓库协作约定

## 语言约定

- 源码必须使用英文。
- 代码注释必须使用英文。
- 用户可见消息、诊断消息、日志和其他程序输出字符串必须使用英文。
- `README.md`、`AGENTS.md`、设计说明等代码外文档必须使用中文。
- 每个功能变更都必须同步更新 Sample，让新行为可以被直接运行和观察。

## 仓库事实

- 默认目标框架是 `net10.0`，语言版本是 C# 14。
- 源生成器项目运行在 `netstandard2.0`，并作为 Roslyn Analyzer 被 Sample 和使用方项目引用。
- 包版本通过 `Directory.Packages.props` 集中管理。
- SDK 版本由 `global.json` 约束，当前要求 `10.0.100`，并允许 `latestFeature` roll-forward。
- 测试框架是 TUnit，测试运行器是 Microsoft.Testing.Platform。

## 目录职责

- `sources/managed` 存放所有库源码，包括 Abstractions、Core、Extensions、Generator、ExitCodes 和 Escapes。
- `samples` 存放可运行示例。新增或调整用户可见能力时，必须在 Sample 中展示对应行为。
- `tests` 存放测试项目。解析器、容器、tokenizer、响应文件、源生成器诊断和绑定行为都应有对应测试。
- `images` 存放 README 和包元数据使用的图片资源。

## 生成器约定

- 新增或调整源生成器诊断时，同步更新 `sources/managed/Kawayi.CommandLine.Generator/AnalyzerReleases.Unshipped.md`。
- 新增命令属性、导出接口或生成代码形状时，优先补充 Generator 测试，再补充 Core 或 Extensions 测试。
- 生成器产生的公共成员和诊断文本仍然属于代码输出，必须使用英文。
- 子命令、绑定、符号和文档导出之间存在依赖关系，修改其中一项时需要检查另外几项是否仍一致。

## 测试与验证

- 优先运行与变更相关的测试项目，合并前运行 `dotnet test Kawayi.CommandLine.slnx`。
- 修改解析行为时至少运行 Core 测试和 Sample 中对应命令。
- 修改源生成器时至少运行 `tests/Kawayi.CommandLine.Generator.Tests/Kawayi.CommandLine.Generator.Tests.csproj`。
- 涉及 Sample 行为验证时，串行运行 `dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- ...`，避免并行构建导致临时文件锁。
- 本地仓库没有 remote 时，SourceLink 可能输出 repository 或 source control warning；除非构建失败，否则不要把这类 warning 当作功能失败。

## 文档约定

- README 描述必须以当前 Sample、测试和源码中已经存在的能力为准。
- 不要宣传未实现或未验证的分发渠道；当前安装说明以源码引用和 `ProjectReference` 为准。
- 文档中的 C# 示例、命令输出、用户可见字符串仍应保持英文，说明文字使用中文。

## 工作区约定

- 不要覆盖用户未提交的改动。
- 只修改当前任务相关文件。
- 避免无关格式化、重命名或项目文件调整。
