using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;

using Bnfour.RitsuFuse.Proper;

namespace Bnfour.RitsuFuse.ConsoleApp;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Ritsu FUSE console launcher");

        var customVersionOption = new Option<bool>("--version", "Display versions for this app and used library");
        customVersionOption.AddAlias("-v");

        rootCommand.AddGlobalOption(customVersionOption);
        rootCommand.SetHandler(boolean =>
        {
            if (boolean)
            {
                Console.WriteLine($"Console app {GetVersion().ToString(3)}, library {RitsuFuseWrapper.GetVersion().ToString(3)}");
            }
        }, customVersionOption);

        // the default handler for --version takes over custom stuff,
        // so it is excluded from the pipeline here
        var commandLineBuilder = new CommandLineBuilder(rootCommand);
        // basically, UseDefaults but without the version option
        // because it takes over our custom handler
        commandLineBuilder
            .UseHelp()
            .UseEnvironmentVariableDirective()
            .UseParseDirective()
            .UseSuggestDirective()
            .RegisterWithDotnetSuggest()
            .UseTypoCorrections()
            .UseParseErrorReporting()
            .UseExceptionHandler()
            .CancelOnProcessTermination();
        // TODO consider whether these options are needed

        // TODO actually start the wrapper with settings collected from the arguments
        new RitsuFuseWrapper().Start(new RitsuFuseSettings
        {
            TargetFolder = "/home/me/Downloads/ritsu",
            FileSystemRoot = "/tmp/ayaya",
            PreventRepeats = true,
            UseQueue = true,
            Verbose = false,
            LogAction = Console.WriteLine
        });

        var parser = commandLineBuilder.Build();
        return await parser.InvokeAsync(args);
    }
    private static Version GetVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Unable to determine app version");
}
