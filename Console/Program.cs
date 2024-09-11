﻿using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;

using Bnfour.RitsuFuse.Proper;

namespace Bnfour.RitsuFuse.Console;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Ritsu FUSE console launcher");

        var customVersionOption = new Option<bool>("--version", "Display versions for this app and used library");
        rootCommand.AddGlobalOption(customVersionOption);

        rootCommand.SetHandler(boolean =>
        {
            if (boolean)
            {
                // TODO the "Console" name for this app is dubious, change to ConsoleApp
                System.Console.WriteLine($"Console app {GetVersion().ToString(3)}, library {RitsuFuseWrapper.GetVersion().ToString(3)}");
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

        var parser = commandLineBuilder.Build();
        return await parser.InvokeAsync(args);
    }
    private static Version GetVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? throw new ApplicationException("Unable to determine library version");
}
