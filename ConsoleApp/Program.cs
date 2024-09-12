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

        var targetFolderArgument = new Argument<FileInfo>("target folder", "Folder with files to create a random symlink to. Must contain at least 2 files.");
        var rootFolderArgument = new Argument<FileInfo>("file system root folder", "Folder to host the file system. Must exist and be empty.");

        var timeoutOption = new Option<uint>("--timeout", () => 100, "Time (in milliseconds) between requests to continue returning the same target. Most apps read the link more than once.");

        var verboseOption = new Option<bool>("--verbose", "Display diagnostic messages.");

        var noRepeatsOption = new Option<bool>("--no-repeats", "Prevents the same file being targeted twice in a row.");
        var queueOption = new Option<bool>("--queue", "Use shuffled queue instead of full random. Returns each file in random order once before repeating.");

        var linkNameOption = new Option<string>("--link-name", () => "ritsu", "Name of the symlink in the file system folder.");

        rootCommand.AddArgument(targetFolderArgument);
        rootCommand.AddArgument(rootFolderArgument);
        rootCommand.AddOption(timeoutOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(noRepeatsOption);
        rootCommand.AddOption(queueOption);
        rootCommand.AddOption(linkNameOption);

        rootCommand.SetHandler((isVersion, settings) =>
        {
            if (isVersion)
            {
                Console.WriteLine($"Console app {GetVersion().ToString(3)}, library {RitsuFuseWrapper.GetVersion().ToString(3)}");
            }
            else
            {
                // TODO launch the app
            }
        }, customVersionOption, new SettingsBinder(targetFolderArgument, rootFolderArgument, timeoutOption, verboseOption, noRepeatsOption, queueOption, linkNameOption));

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
        var parser = commandLineBuilder.Build();
        return await parser.InvokeAsync(args);
    }
    private static Version GetVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Unable to determine app version");
}
