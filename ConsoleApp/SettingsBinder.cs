using System.CommandLine;
using System.CommandLine.Binding;

using Bnfour.RitsuFuse.Proper;

namespace Bnfour.RitsuFuse.ConsoleApp;

/// <summary>
/// Binds command-line options to a <see cref="RitsuFuseSettings"/> instance.
/// </summary>
internal class SettingsBinder(
    Argument<FileInfo> targetFolderArgument,
    Argument<FileInfo> rootFolderArgument,
    Option<uint> timeoutOption,
    Option<bool> verboseOption,
    Option<bool> noRepeatsOption,
    Option<bool> queueOption,
    Option<string> linkNameOption): BinderBase<RitsuFuseSettings>
{
    protected override RitsuFuseSettings GetBoundValue(BindingContext bindingContext)
        => new()
        {
            TargetFolder = bindingContext.ParseResult.GetValueForArgument(targetFolderArgument).FullName,
            FileSystemRoot = bindingContext.ParseResult.GetValueForArgument(rootFolderArgument).FullName,

            Timeout = TimeSpan.FromMilliseconds(bindingContext.ParseResult.GetValueForOption(timeoutOption)),

            Verbose = bindingContext.ParseResult.GetValueForOption(verboseOption),

            PreventRepeats = bindingContext.ParseResult.GetValueForOption(noRepeatsOption),
            UseQueue = bindingContext.ParseResult.GetValueForOption(queueOption),

            LinkName = bindingContext.ParseResult.GetValueForOption(linkNameOption)!,

            LogAction = bindingContext.ParseResult.GetValueForOption(verboseOption)
                ? Console.WriteLine
                : null
        };
}
