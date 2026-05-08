# Kawayi.CommandLine

![打碎旧世界,建设新世界](https://raw.githubusercontent.com/moegodot/NewProject-CS/refs/heads/master/images/readme_image_1.png)

Kawayi.CommandLine 是一个面向 .NET 10 和 C# 14 的属性驱动命令行解析库。它把命令、参数、选项、子命令和帮助文本声明在普通 C# 类型上，并通过源生成器生成文档、符号、解析和绑定代码，让命令行入口保持清晰、可测试、可组合。

当前仓库以源码引用为主要使用方式，暂不假设 NuGet 包已经发布。

## 功能亮点

- 使用 `[Command]`、`[Argument]`、`[Property]`、`[Subcommand]` 描述命令行界面，并一站式生成文档、符号、解析和绑定代码。
- 支持位置参数、长选项、短选项、`--name=value` 与 `-xVALUE` 内联值、显式布尔值和裸布尔开关。
- 支持 `--` 选项终止符转发后续 token，也支持用反斜杠转义把 `\-value` 强制解析为位置参数。
- 支持多级子命令、可见和隐藏别名、隐藏参数与隐藏选项。
- 支持响应文件，`@file` 会按文件中的 token 展开。
- 支持默认值、值数量范围、验证器、枚举可能值和手工可能值描述。
- 支持帮助输出、版本输出、ANSI 样式表、自定义输出目标和调试输出。
- 支持常见基础类型解析，包括整数、浮点数、`bool`、`Guid`、`Uri`、`DateTime`、`DateTimeOffset`、`DateOnly`、`TimeOnly` 和 `enum`。
- 支持不可变容器解析，包括 `ImmutableArray<T>`、`ImmutableList<T>`、`ImmutableHashSet<T>`、`ImmutableDictionary<TKey, TValue>`、`ImmutableSortedDictionary<TKey, TValue>` 等。
- 通过 `[Command]` 生成的绑定逻辑和 `Bind<T>()` 把解析结果绑定回命令对象；也可以用 `[Bindable]` 做更细粒度的显式导出。

## 项目结构

- `sources/managed/Kawayi.CommandLine.Abstractions`：公共抽象、定义模型、解析结果、样式、token 和导出接口。
- `sources/managed/Kawayi.CommandLine.Core`：tokenizer、响应文件展开、解析器、构建器、属性定义和基础类型解析。
- `sources/managed/Kawayi.CommandLine.Extensions`：面向解析结果和可解析类型的便捷扩展。
- `sources/managed/Kawayi.CommandLine.Generator`：Roslyn 源生成器，负责导出文档、符号、解析器和绑定代码。
- `sources/managed/Kawayi.ExitCodes`：常用退出码常量和校验工具。
- `sources/managed/Kawayi.Escapes`：字符串转义规则，服务于容器解析和 dash 前缀 token 转义。
- `samples/Kawayi.CommandLine.Sample`：功能展示项目，新能力应同步在这里体现。
- `tests`：TUnit 测试，覆盖 Core、Generator、ExitCodes 和 Escapes。

## 环境要求

- .NET SDK `10.0.100` 或更新的兼容版本。仓库通过 `global.json` 启用 `rollForward: latestFeature` 和预览 SDK。
- C# 14，默认目标框架为 `net10.0`。
- 测试使用 TUnit 和 Microsoft.Testing.Platform。
- 包版本通过 `Directory.Packages.props` 集中管理。

## 安装

在应用项目中通过 `ProjectReference` 引用源码项目。应用代码通常需要引用 Abstractions、Core、Extensions，并把 Generator 作为 Analyzer 接入：

```xml
<ItemGroup>
  <ProjectReference Include="..\Kawayi.CommandLine\sources\managed\Kawayi.CommandLine.Abstractions\Kawayi.CommandLine.Abstractions.csproj" />
  <ProjectReference Include="..\Kawayi.CommandLine\sources\managed\Kawayi.CommandLine.Core\Kawayi.CommandLine.Core.csproj" />
  <ProjectReference Include="..\Kawayi.CommandLine\sources\managed\Kawayi.CommandLine.Extensions\Kawayi.CommandLine.Extensions.csproj" />
  <ProjectReference Include="..\Kawayi.CommandLine\sources\managed\Kawayi.CommandLine.Generator\Kawayi.CommandLine.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

如果应用只需要手写 `CliSchemaBuilder` 和调用 `CliSchemaParser`，可以不接入 Generator。若使用属性生成文档、符号、解析和绑定代码，命令类型必须是 `partial`。

## 快速上手

先定义一个命令模型：

```csharp
using System.Collections.Immutable;
using Kawayi.CommandLine.Core.Attributes;

namespace MyApp;

[Command]
public partial class BuildCommand
{
    /// <summary>
    /// Project path
    /// </summary>
    /// <remarks>
    /// Required positional argument.
    /// </remarks>
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Output format
    /// </summary>
    /// <remarks>
    /// Selects the emitted report format.
    /// </remarks>
    [Property(require: true, valueName: "format")]
    [LongAlias("format")]
    [ShortAlias("f")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Enable verbose logging
    /// </summary>
    /// <remarks>
    /// Emits additional progress details.
    /// </remarks>
    [Property]
    [LongAlias("verbose")]
    [ShortAlias("v")]
    public bool Verbose { get; set; }

    /// <summary>
    /// Build tags
    /// </summary>
    /// <remarks>
    /// Repeated option collected into an immutable array.
    /// </remarks>
    [Property]
    [LongAlias("tag")]
    public ImmutableArray<string> Tags { get; set; }
}
```

再在入口中解析并绑定：

```csharp
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;
using Kawayi.CommandLine.Extensions;
using MyApp;

var options = ParsingOptions.Create<BuildCommand>(
    "Build command",
    "Builds a project and emits a report.",
    "https://example.com/");

var tokenizer = new Tokenizer();
var rawTokens = tokenizer.Tokenize([.. args]);
var tokens = new ResponseFileReplacer(tokenizer).Replace(rawTokens);

var builder = BuildCommand.ExportParsing(options);
var result = ContinueSubcommands(CliSchemaParser.CreateParsing(options, tokens, builder.Build()));

return result switch
{
    HelpFlagsDetected help => Run(help.FlagAction, 0),
    VersionFlagsDetected version => Run(version.FlagAction, 0),
    ParsingFinished<Cli> finished => RunCommand(finished.Result.Bind<BuildCommand>()),
    GotError error => Report(error),
    _ => 1
};

static ParsingResult ContinueSubcommands(ParsingResult result)
{
    while (result is Subcommand subcommand)
    {
        result = subcommand.ContinueParseAction();
    }

    return result;
}

static int Run(Action action, int exitCode)
{
    action();
    return exitCode;
}

static int RunCommand(BuildCommand command)
{
    Console.WriteLine($"Project: {command.Project}");
    Console.WriteLine($"Format: {command.Format}");
    return 0;
}

static int Report(GotError error)
{
    Console.Error.WriteLine(error.Exception?.Message ?? error.ToString());
    return 1;
}
```

生成器会从 XML 文档注释中读取摘要和详细说明，生成帮助文档、符号表、解析入口和绑定逻辑。`[Command]` 是推荐的一站式命令属性，会启用 `ExportParsing`、`CreateParsing`、`Documents`、`Symbols` 和绑定生成；`[ExportDocument]`、`[ExportSymbols]`、`[ExportParsing]`、`[Bindable]` 仍可用于只需要部分导出的高级场景。需要在解析后调整可能值、默认值或验证规则时，可以先取得 `ExportParsing(options)` 返回的 `CliSchemaBuilder`，再修改对应定义，最后调用 `Build()`。

生成的 schema 导出接口名是 `ICliSchemaExporter`。解析器会执行默认值工厂、必填检查和验证器；绑定阶段只用解析结果中存在的值覆盖命令对象，因此未输入且没有默认值工厂的可选成员会保留类型构造或属性初始化时的值。

## CliSchemaParser 解析模型

`[Command]` 生成的 `ExportParsing(options)` 会把 `Symbols` 中的 `ParameterDefinition`、`PropertyDefinition` 和 `CommandDefinition` 放入 `CliSchemaBuilder`。`CliSchemaBuilder.Build()` 冻结为不可变 `CliSchema` 时，会保留 `GeneratedFrom` 类型，按 `[Argument(position)]` 生成的位置参数顺序保存参数，把属性主名、长别名和短别名展开为可匹配的 option token，并为每个普通子命令连接子 schema。隐藏参数、隐藏选项、隐藏别名和隐藏子命令别名只影响帮助可见性，仍然会进入 schema 并保持可解析。

解析从 token 扫描开始。`--help`、`-h`、`help`、`--version`、`-V` 和 `version` 会在当前 scope 内短路为帮助或版本结果；普通 `ArgumentOrCommandToken` 会优先匹配子命令，再作为位置参数候选；`ArgumentToken` 是被转义或位于 `--` 之后的参数，不会匹配子命令。未知 option 不会被字符串属性随意吞掉，最终会返回 `UnknownArgumentDetected`。

位置参数按声明顺序和 `ValueRange` 分配。当前参数会尽量贪婪消费 token，但解析器会为后续参数保留它们的最小值数量；值数量不足或过多时返回 `InvalidArgumentDetected`。dash 前缀的 token 通常是 option，但当当前位置参数或当前属性需要数值类型且 token 能按 invariant culture 解析为数值时，`-1`、`-1.5` 这类 token 会被当作值处理。

属性的默认取值数量来自属性类型。`bool` 的默认 `NumArgs` 是 `ZeroOrOne`，支持 `--flag` 作为 `true`，支持 `--flag=false` 或下一个 token 为 `true`/`false` 时的试探消费；如果下一个 token 不是布尔值或是子命令名，布尔属性不会消费它。不可变容器属性的默认 `NumArgs` 是 `ZeroOrMore`，其他属性默认是 `One`；显式 `[ValueRange]` 会覆盖这些默认值。长选项支持 `--name=value`，短选项支持 `-xVALUE`，内联值不会继续消费下一个 token。

支持的标量目标类型包括所有整数类型、`float`、`double`、`decimal`、`bool`、`string`、`Guid`、`Uri`、`DateTime`、`DateTimeOffset`、`DateOnly`、`TimeOnly` 和 `enum`。整数使用 `NumberStyles.Integer` 和 invariant culture，浮点数与 `decimal` 使用 `NumberStyles.Float` 和 invariant culture，枚举解析忽略大小写。`string` 取最后一个 token 的文本，空 token 会得到空字符串。

支持的不可变容器包括 `ImmutableArray<T>`、`ImmutableList<T>`、`ImmutableQueue<T>`、`ImmutableStack<T>`、`ImmutableHashSet<T>`、`ImmutableSortedSet<T>`、`ImmutableDictionary<TKey, TValue>` 和 `ImmutableSortedDictionary<TKey, TValue>`。序列容器逐 token 解析元素；字典容器要求每个 token 形如 `key=value`，其中 `\=` 表示字面等号，重复 key 会以后出现的值覆盖先出现的值。

scope 内显式解析结束后，解析器会计算有效值。若用户提供了显式值，就使用显式值；若没有显式值且 definition 带有 `DefaultValueFactory`，就调用工厂并把返回值视为有效值。随后依次检查 `Requirement`、`RequirementIfNull` 和 `Validation`：`Requirement` 要求必须存在显式值或默认值，`RequirementIfNull` 要求有效值不能为 `null`，`Validation` 会同时作用于显式值和默认值，返回非空错误文本或抛出异常时都会变成 `FailedValidation`。

`--` 是 option terminator。Tokenizer 会把它后面的原始输入变成 `ArgumentToken`；`CliSchemaParser` 会把这些 token 放入 `Cli.ToProgramArguments`，它们不再参与当前 schema 的参数、属性或子命令解析。响应文件替换也不会展开 `--` 后的 `ArgumentToken`，因此 `-- @file` 会把 `@file` 原样转发。

普通子命令会先完成父 scope，然后返回 `Subcommand`；调用 `ContinueParseAction()` 后才解析子 scope，`ParseRecursively()` 可以持续展开到最终结果。`[Subcommand(global: true)]` 会把子命令的参数、属性和下级子命令提升到父 schema，并在绑定阶段总是实例化该子对象。生成的绑定逻辑只读取当前 `Cli` 结果中存在的值：已解析值和默认工厂值会覆盖对象属性，缺席且没有默认工厂的可选成员保留构造器或属性初始化值，未选择的普通子命令会绑定为 `null`。

## Sample

Sample 覆盖了当前主要能力。以下命令请串行运行，避免并行构建时出现临时文件锁：

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- --help
```

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json --execution-mode background
```

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format=json --env=region=cn --execution-mode=background
```

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload extra-a extra-b --format json --verbose
```

```bash
CLI_DEBUG=1 NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json serve localhost --daemon watch --interval 5
```

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json serve localhost watch --interval 5 changes
```

缺失 scalar 选项值会报告错误：

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json --threshold
```

短选项内联值、dash 前缀参数转义和 `--` 转发示例：

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json serve localhost watch --interval 5 -k-L/bin/foo.a
```

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- '\-serve' --format json
```

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json -- --child -x
```

响应文件示例：

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json @sample-response.txt
```

`--` 之后的 `@sample-response.txt` 会作为转发参数保留，不再展开为响应文件：

```bash
NO_COLOR=1 dotnet run --project ./samples/Kawayi.CommandLine.Sample/Kawayi.CommandLine.Sample.csproj -- payload --format json -- @sample-response.txt
```

其中 `sample-response.txt` 按行保存 token，例如：

```text
--secret-token
hush
--verbose
true
serve
localhost
watch
--interval
3
```

## 开发与测试

构建整个解决方案：

```bash
dotnet build Kawayi.CommandLine.slnx
```

运行全部测试：

```bash
dotnet test --solution Kawayi.CommandLine.slnx
```

运行单个测试项目：

```bash
dotnet test --project tests/Kawayi.CommandLine.Core.Tests/Kawayi.CommandLine.Core.Tests.csproj
dotnet test --project tests/Kawayi.CommandLine.Generator.Tests/Kawayi.CommandLine.Generator.Tests.csproj
```

本地仓库没有配置 remote 时，SourceLink 可能报告 repository 或 source control warning；这通常是本地环境提示，不代表命令行解析功能失败。

## 许可证与反馈

本项目使用 AGPL-3.0-or-later 许可证，详情见 `LICENSE`。

问题反馈、讨论和后续入口请使用项目主页：<https://github.com/moegodot/>。
