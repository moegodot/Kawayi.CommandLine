// Example invocations:
// dotnet run --project samples/Kawayi.CommandLine.Sample -- --help
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json --execution-mode background
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format=json --env=region=cn --execution-mode=background
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json --threshold -1
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json serve localhost watch error --interval 5
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json --verbose serve localhost watch --interval 5 changes
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json --trace-id-prefix demo --color-output true
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload hidden-profile extra-a extra-b --format xml --verbose true --retries 4 --tag alpha --tag beta --env region=cn --env tier=prod serve localhost --daemon false watch --interval 5 --once true --sink stdout changes
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json serve localhost watch --interval 5 --sink -L/bin/foo.a
// dotnet run --project samples/Kawayi.CommandLine.Sample -- payload --format json @sample-response.txt
// sample-response.txt stores one token per line, for example:
// --secret-token
// hush
// --verbose
// true
// serve
// localhost
// watch
// --interval
// 3

using System.Collections;
using System.Collections.Immutable;
using Kawayi.CommandLine.Abstractions;
using Kawayi.CommandLine.Core;
using Kawayi.CommandLine.Core.Attributes;
using Kawayi.CommandLine.Extensions;

namespace Kawayi.CommandLine.Sample;

/// <summary>
/// Demonstrates enum parsing in the sample root command.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Execute work in the foreground.
    /// </summary>
    Foreground = 0,
    /// <summary>
    /// Execute work in the background.
    /// </summary>
    Background = 1,
    /// <summary>
    /// Validate the workflow without performing side effects.
    /// </summary>
    DryRun = 2
}

internal static class Program
{
    private static readonly ProgramInformation ProgramMetadata = new(
        "workspace-demo",
        new Document(
            "Attribute-first command line showcase",
            "Demonstrates attribute-driven command metadata plus post-generation ParsingBuilder augmentation."),
        new Version(1, 0, 0),
        "https://github.com/moegodot/");

    private static int Main(string[] args)
    {
        var effectiveArguments = args.Length == 0 ? ["--help"] : args;
        var parsingOptions = CreateOptions(Console.Out);

        if (args.Length == 0)
        {
            PrintOverview<WorkspaceCommand>(Console.Out, parsingOptions);
        }

        var tokenizer = new Tokenizer();
        var rawTokens = tokenizer.Tokenlize([.. effectiveArguments]);
        var tokens = new ResponseFileReplacer(tokenizer).Replace(rawTokens);

        var builder = WorkspaceCommand.ExportParsing(parsingOptions);
        AugmentGeneratedBuilder(builder);

        var effectiveBuilder = ShouldUseRelaxedSubcommandHelpRoute(tokens)
            ? CloneForScopedHelp(builder)
            : builder;
        var result = ParsingBuilder.CreateParsing(parsingOptions, tokens, effectiveBuilder.Build());
        return HandleResult(result);
    }

    private static ParsingOptions CreateOptions(TextWriter output)
    {
        var styleTable = StyleTable.Default with
        {
            HelpTitleStyle = new Style(Color.Emerald, Color.None, true, false, false),
            DebugTitleStyle = new Style(Color.Amber, Color.None, true, false, false)
        };

        return new ParsingOptions(
            ProgramMetadata,
            ParsingOptions.DefaultVersionFlags,
            ParsingOptions.DefaultHelpFlags,
            output,
            ParsingOptions.DefaultStyle,
            ParsingOptions.DefaultDebug,
            styleTable);
    }

    private static void PrintOverview<T>(TextWriter output, ParsingOptions options)
        where T : IDocumentExporter, ISymbolExporter, IParsingExporter, Kawayi.CommandLine.Abstractions.IParsable<T>, new()
    {
        var warmupResult = T.CreateParsing(options, [new LongOptionToken("help")], new T());
        var parserSurface = T.ExportParsing(options);

        output.WriteLine("Kawayi.CommandLine Attribute Max Demo");
        output.WriteLine($"Documents: {T.Documents.Count}");
        output.WriteLine($"Symbols: {T.Symbols.Length}");
        output.WriteLine($"Root surface: {parserSurface.Argument.Count} arguments, {parserSurface.Properties.Count} options, {parserSurface.SubcommandDefinitions.Count} subcommands");
        output.WriteLine($"Warm-up via IParsable<T>: {warmupResult.GetType().Name}");
        output.WriteLine();
    }

    private static void AugmentGeneratedBuilder(IParsingBuilder rootBuilder)
    {
        rootBuilder.Properties["format"] = rootBuilder.Properties["format"] with
        {
            PossibleValues = new CountablePossibleValues<string>(["json", "yaml", "toml"])
        };

        rootBuilder.Properties["retries"] = rootBuilder.Properties["retries"] with
        {
            DefaultValueFactory = static _ => 3,
            Validation = static value => (int)value < 0 ? "retries must be zero or greater." : null
        };

        rootBuilder.Properties["env"] = rootBuilder.Properties["env"] with
        {
            DefaultValueFactory = static _ => ImmutableDictionary<string, string>.Empty
        };

        rootBuilder.Properties["trace-id-prefix"] = rootBuilder.Properties["trace-id-prefix"] with
        {
            DefaultValueFactory = static _ => "workspace"
        };

        var serveBuilder = GetRequiredSubcommandBuilder(rootBuilder, "serve");
        serveBuilder.Properties["port"] = serveBuilder.Properties["port"] with
        {
            DefaultValueFactory = static _ => 8080,
            Validation = static value => (int)value is < 1 or > 65535 ? "port must be between 1 and 65535." : null
        };

        var watchBuilder = GetRequiredSubcommandBuilder(serveBuilder, "watch");
        watchBuilder.Properties["interval"] = watchBuilder.Properties["interval"] with
        {
            Validation = static value => (int)value <= 0 ? "interval must be greater than zero." : null
        };
        watchBuilder.Properties["sink"] = watchBuilder.Properties["sink"] with
        {
            PossibleValues = new DescripablePossibleValues("stdout, file or any custom sink plugin")
        };
    }

    private static IParsingBuilder GetRequiredSubcommandBuilder(IParsingBuilder builder, string key)
    {
        if (builder.Subcommands.TryGetValue(key, out var childBuilder) && childBuilder is not null)
        {
            return childBuilder;
        }

        throw new InvalidOperationException($"Expected subcommand builder '{key}' to exist.");
    }

    private static bool ShouldUseRelaxedSubcommandHelpRoute(ImmutableArray<Token> tokens)
    {
        if (tokens.Length < 2 || !IsHelpToken(tokens[^1]))
        {
            return false;
        }

        return tokens[..^1].All(static token => token is ArgumentOrCommandToken);
    }

    private static bool IsHelpToken(Token token)
    {
        return token switch
        {
            LongOptionToken { Value: "help" } => true,
            ShortOptionToken { Value: "h" } => true,
            ArgumentOrCommandToken { Value: "help" } => true,
            _ => false
        };
    }

    private static IParsingBuilder CloneForScopedHelp(IParsingBuilder source)
    {
        var clone = new ParsingBuilder(source.ParsingOptions);

        foreach (var (key, definition) in source.SubcommandDefinitions)
        {
            clone.SubcommandDefinitions[key] = definition;
        }

        foreach (var (key, property) in source.Properties)
        {
            clone.Properties[key] = property with
            {
                Requirement = false
            };
        }

        foreach (var argument in source.Argument)
        {
            clone.Argument.Add(argument with
            {
                ValueRange = new ValueRange(0, argument.ValueRange.Maximum),
                Requirement = false
            });
        }

        foreach (var (key, childBuilder) in source.Subcommands)
        {
            clone.Subcommands[key] = CloneForScopedHelp(childBuilder);
        }

        return clone;
    }

    private static int HandleResult(ParsingResult result)
    {
        var terminalResult = ContinueSubcommands(result);

        switch (terminalResult)
        {
            case HelpFlagsDetected helpFlagsDetected:
                helpFlagsDetected.FlagAction();
                return 0;
            case VersionFlagsDetected versionFlagsDetected:
                versionFlagsDetected.FlagAction();
                return 0;
            case ParsingFinished<IParsingResultCollection> parsingFinished:
                PrintSuccess(parsingFinished.Result.Bind<WorkspaceCommand>(), Console.Out);
                return 0;
            case InvalidArgumentDetected invalidArgumentDetected:
                PrintError("Invalid argument", $"{invalidArgumentDetected.Argument}: expected {invalidArgumentDetected.Expect}", invalidArgumentDetected.Exception);
                return 1;
            case UnknownArgumentDetected unknownArgumentDetected:
                PrintError("Unknown argument", unknownArgumentDetected.UnknownArgument, unknownArgumentDetected.Exception);
                return 1;
            case FailedValidation failedValidation:
                PrintError("Validation failed", $"{failedValidation.Argument}: {failedValidation.Reason}", failedValidation.Exception);
                return 1;
            case GotError gotError:
                PrintError("Unexpected parser error", gotError.Exception?.Message ?? "Unknown parser failure.", gotError.Exception);
                return 1;
            default:
                PrintError("Unexpected parser result", terminalResult.ToString(), null);
                return 1;
        }
    }

    private static ParsingResult ContinueSubcommands(ParsingResult result)
    {
        var current = result;

        while (current is Subcommand subcommand)
        {
            current = subcommand.ContinueParseAction();
        }

        return current;
    }

    private static void PrintSuccess(WorkspaceCommand command, TextWriter output)
    {
        output.WriteLine("Parse succeeded.");
        output.WriteLine($"Command path: {BuildCommandPath(command)}");
        output.WriteLine();
        PrintBoundCommand(command, output);
    }

    private static void PrintBoundCommand(WorkspaceCommand command, TextWriter output)
    {
        output.WriteLine("Bound command model:");
        output.WriteLine($"  Input: {DescribeValue(command.Input)}");
        output.WriteLine($"  Profile: {DescribeValue(command.Profile)}");
        output.WriteLine($"  Extras: {DescribeValue(command.Extras)}");
        output.WriteLine($"  Format: {DescribeValue(command.Format)}");
        output.WriteLine($"  Verbose: {DescribeValue(command.Verbose)}");
        output.WriteLine($"  Retries: {DescribeValue(command.Retries)}");
        output.WriteLine($"  ExecutionMode: {DescribeValue(command.ExecutionMode)}");
        output.WriteLine($"  Threshold: {DescribeValue(command.Threshold)}");
        output.WriteLine($"  RequestId: {DescribeValue(command.RequestId)}");
        output.WriteLine($"  Endpoint: {DescribeValue(command.Endpoint)}");
        output.WriteLine($"  StartDate: {DescribeValue(command.StartDate)}");
        output.WriteLine($"  StartTime: {DescribeValue(command.StartTime)}");
        output.WriteLine($"  Tags: {DescribeValue(command.Tags)}");
        output.WriteLine($"  Env: {DescribeValue(command.Env)}");
        output.WriteLine($"  SecretToken: {(command.SecretToken is null ? "null" : "(set)")}");
        output.WriteLine("  GlobalOptions: instantiated");
        output.WriteLine($"    TraceIdPrefix: {DescribeValue(command.GlobalOptions.TraceIdPrefix)}");
        output.WriteLine($"    ColorOutput: {DescribeValue(command.GlobalOptions.ColorOutput)}");
        output.WriteLine($"  Serve: {DescribeSelected(command.Serve is not null)}");

        if (command.Serve is null)
        {
            return;
        }

        output.WriteLine($"    Host: {DescribeValue(command.Serve.Host)}");
        output.WriteLine($"    Port: {DescribeValue(command.Serve.Port)}");
        output.WriteLine($"    Daemon: {DescribeValue(command.Serve.Daemon)}");
        output.WriteLine($"    Watch: {DescribeSelected(command.Serve.Watch is not null)}");

        if (command.Serve.Watch is not null)
        {
            output.WriteLine($"      Pattern: {DescribeValue(command.Serve.Watch.Pattern)}");
            output.WriteLine($"      Interval: {DescribeValue(command.Serve.Watch.Interval)}");
            output.WriteLine($"      Once: {DescribeValue(command.Serve.Watch.Once)}");
            output.WriteLine($"      Sink: {DescribeValue(command.Serve.Watch.Sink)}");
        }
    }

    private static void PrintError(string title, string message, Exception? exception)
    {
        Console.Error.WriteLine(title);
        Console.Error.WriteLine(message);

        if (exception is not null)
        {
            Console.Error.WriteLine(exception.Message);
        }
    }

    private static string BuildCommandPath(WorkspaceCommand command)
    {
        if (command.Serve?.Watch is not null)
        {
            return "serve -> watch";
        }

        return command.Serve is not null ? "serve" : "<root>";
    }

    private static string DescribeSelected(bool selected) => selected ? "selected" : "not selected";

    private static string DescribeValue(object? value)
    {
        if (value is not null && IsDefaultImmutableArray(value))
        {
            return "[]";
        }

        return value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            bool boolean => boolean ? "true" : "false",
            DateOnly dateOnly => dateOnly.ToString("O"),
            TimeOnly timeOnly => timeOnly.ToString("O"),
            IEnumerable enumerable when value is not string => DescribeEnumerable(enumerable),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool IsDefaultImmutableArray(object value)
    {
        var type = value.GetType();

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ImmutableArray<>))
        {
            return false;
        }

        return (bool)(type.GetProperty(nameof(ImmutableArray<int>.IsDefault))?.GetValue(value) ?? false);
    }

    private static string DescribeEnumerable(IEnumerable value)
    {
        var parts = new List<string>();

        foreach (var item in value)
        {
            parts.Add(DescribeEnumerableItem(item));
        }

        return $"[{string.Join(", ", parts)}]";
    }

    private static string DescribeEnumerableItem(object? item)
    {
        if (item is null)
        {
            return "null";
        }

        var itemType = item.GetType();

        if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var key = itemType.GetProperty("Key")!.GetValue(item);
            var value = itemType.GetProperty("Value")!.GetValue(item);
            return $"{DescribeValue(key)}={DescribeValue(value)}";
        }

        return DescribeValue(item);
    }
}

/// <summary>
/// Root command used by the attribute showcase.
/// </summary>
[Command]
public partial class WorkspaceCommand
{
    /// <summary>
    /// Primary input payload
    /// </summary>
    /// <remarks>
    /// Required root positional argument. The parser prefers subcommands over positional matches, so
    /// the sample keeps this first value unambiguous.
    /// </remarks>
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Hidden profile selector
    /// </summary>
    /// <remarks>
    /// Optional positional argument hidden from help output to demonstrate visible:false on arguments.
    /// </remarks>
    [Argument(1, visible: false)]
    [ValueRange(0, 1)]
    public string? Profile { get; set; }

    /// <summary>
    /// Additional trailing values
    /// </summary>
    /// <remarks>
    /// Variadic argument that demonstrates greedy positional binding once the fixed arguments are satisfied.
    /// </remarks>
    [Argument(2)]
    [ValueRange(0, int.MaxValue)]
    public ImmutableArray<string> Extras { get; set; }

    /// <summary>
    /// Enable verbose logging
    /// </summary>
    /// <remarks>
    /// Demonstrates an explicit boolean option with both long and short aliases.
    /// </remarks>
    [Property]
    [LongAlias("verbose")]
    [ShortAlias("v")]
    public bool Verbose { get; set; }

    /// <summary>
    /// Choose the output format
    /// </summary>
    /// <remarks>
    /// Demonstrates a single-value property. Repeating the option causes property count validation to fail
    /// before scalar parsing chooses the last value.
    /// </remarks>
    [Property(require: true, valueName: "format")]
    [ValueRange(1, 1)]
    [LongAlias("format")]
    [LongAlias("output-format", visible: false)]
    [ShortAlias("f")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Retry count
    /// </summary>
    /// <remarks>
    /// Post-generation augmentation sets a default effective value of 3 when the option is omitted.
    /// </remarks>
    [Property]
    [LongAlias("retries")]
    [ShortAlias("r")]
    public int Retries { get; set; }

    /// <summary>
    /// Execution mode
    /// </summary>
    /// <remarks>
    /// Demonstrates enum parsing and automatic enum possible values in help output.
    /// </remarks>
    [Property]
    [LongAlias("execution-mode")]
    [ShortAlias("m")]
    public ExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// Numeric threshold
    /// </summary>
    /// <remarks>
    /// Demonstrates decimal parsing for scalar options.
    /// </remarks>
    [Property]
    [LongAlias("threshold")]
    public decimal Threshold { get; set; }

    /// <summary>
    /// Request identifier
    /// </summary>
    /// <remarks>
    /// Demonstrates Guid parsing.
    /// </remarks>
    [Property]
    [LongAlias("request-id")]
    public Guid RequestId { get; set; }

    /// <summary>
    /// Service endpoint
    /// </summary>
    /// <remarks>
    /// Demonstrates Uri parsing with a visible long alias only.
    /// </remarks>
    [Property]
    [LongAlias("endpoint")]
    public Uri Endpoint { get; set; } = new("https://example.invalid");

    /// <summary>
    /// Start date
    /// </summary>
    /// <remarks>
    /// Demonstrates DateOnly parsing.
    /// </remarks>
    [Property]
    [LongAlias("start-date")]
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Start time
    /// </summary>
    /// <remarks>
    /// Demonstrates TimeOnly parsing.
    /// </remarks>
    [Property]
    [LongAlias("start-time")]
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// Repeated tags
    /// </summary>
    /// <remarks>
    /// Demonstrates ImmutableArray parsing. The hidden short alias still parses but does not appear in help.
    /// </remarks>
    [Property]
    [LongAlias("tag")]
    [ShortAlias("t", visible: false)]
    public ImmutableArray<string> Tags { get; set; }

    /// <summary>
    /// Environment entries
    /// </summary>
    /// <remarks>
    /// Demonstrates ImmutableDictionary parsing with repeated key=value entries.
    /// </remarks>
    [Property(valueName: "key=value")]
    [LongAlias("env")]
    [ShortAlias("e")]
    public ImmutableDictionary<string, string> Env { get; set; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// Hidden secret token
    /// </summary>
    /// <remarks>
    /// Fully hidden property that remains parsable, including through response files.
    /// </remarks>
    [Property(visible: false)]
    [LongAlias("secret-token", visible: false)]
    public string? SecretToken { get; set; }

    /// <summary>
    /// Global runtime options
    /// </summary>
    /// <remarks>
    /// Promoted subcommand members are parsed on the root command surface and the bound object is always instantiated.
    /// </remarks>
    [Subcommand(global: true)]
    public GlobalOptionsCommand GlobalOptions { get; private set; } = new();

    /// <summary>
    /// Server operations
    /// </summary>
    /// <remarks>
    /// First-level subcommand with both visible and hidden aliases.
    /// </remarks>
    [Subcommand]
    [Alias("srv")]
    [Alias("s", visible: false)]
    public ServeCommand? Serve { get; private set; }
}

/// <summary>
/// Root-level promoted options provided through a global subcommand.
/// </summary>
[Command]
public partial class GlobalOptionsCommand
{
    /// <summary>
    /// Trace identifier prefix
    /// </summary>
    /// <remarks>
    /// Parsed at the root command level even though the property belongs to a child command model.
    /// </remarks>
    [Property]
    [LongAlias("trace-id-prefix")]
    public string TraceIdPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Enable color output
    /// </summary>
    /// <remarks>
    /// Demonstrates a promoted boolean option on an always-instantiated child object.
    /// </remarks>
    [Property]
    [LongAlias("color-output")]
    public bool ColorOutput { get; set; }
}

/// <summary>
/// First-level subcommand for server-oriented operations.
/// </summary>
[Command]
public partial class ServeCommand
{
    /// <summary>
    /// Bind host name
    /// </summary>
    /// <remarks>
    /// Required positional argument for the serve subcommand.
    /// </remarks>
    [Argument(0, require: true)]
    [ValueRange(1, 1)]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Listener port
    /// </summary>
    /// <remarks>
    /// Post-generation augmentation adds a default effective value of 8080 and validates the range.
    /// </remarks>
    [Property]
    [LongAlias("port")]
    [ShortAlias("p")]
    public int Port { get; set; }

    /// <summary>
    /// Run as a daemon
    /// </summary>
    /// <remarks>
    /// Demonstrates another explicit boolean option in a nested scope.
    /// </remarks>
    [Property]
    [LongAlias("daemon")]
    public bool Daemon { get; set; }

    /// <summary>
    /// Watch mode
    /// </summary>
    /// <remarks>
    /// Nested subcommand that demonstrates recursive handoff and scoped help.
    /// </remarks>
    [Subcommand]
    [Alias("tail")]
    [Alias("wt", visible: false)]
    public WatchCommand? Watch { get; private set; }
}

/// <summary>
/// Second-level subcommand for watch-style workflows.
/// </summary>
[Command]
public partial class WatchCommand
{
    /// <summary>
    /// Optional watch pattern
    /// </summary>
    /// <remarks>
    /// Demonstrates an optional positional argument with generated validator metadata inside the second-level subcommand.
    /// </remarks>
    [Argument(0)]
    [ValueRange(0, 1)]
    [Validator(nameof(ValidatePattern))]
    public string? Pattern { get; set; }

    /// <summary>
    /// Validates the optional watch pattern.
    /// </summary>
    /// <param name="pattern">The parsed watch pattern.</param>
    /// <returns>An error message when the pattern is rejected; otherwise, <see langword="null" />.</returns>
    public static string? ValidatePattern(string? pattern)
    {
        return pattern is "error" ? "error" : null;
    }

    /// <summary>
    /// Polling interval
    /// </summary>
    /// <remarks>
    /// Required property validated after parsing. A value of 0 triggers FailedValidation.
    /// </remarks>
    [Property(require: true)]
    [LongAlias("interval")]
    [ShortAlias("i")]
    public int Interval { get; set; }

    /// <summary>
    /// Run once and exit
    /// </summary>
    /// <remarks>
    /// Demonstrates a simple nested explicit boolean option.
    /// </remarks>
    [Property]
    [LongAlias("once")]
    public bool Once { get; set; }

    /// <summary>
    /// Output sink
    /// </summary>
    /// <remarks>
    /// Post-generation augmentation adds descriptive possible values for help output.
    /// </remarks>
    [Property]
    [LongAlias("sink")]
    [ShortAlias("k")]
    public string Sink { get; set; } = "stdout";
}
