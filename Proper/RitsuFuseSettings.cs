namespace Bnfour.RitsuFuse.Proper;

/// <summary>
/// Holds settings provided by the user.
/// Validation is done elsewhere.
/// </summary>
public class RitsuFuseSettings
{
    #region required settings
    /// <summary>
    /// Path to the folder to create a FUSE in.
    /// Should be an existing empty folder.
    /// </summary>
    public required string FileSystemRoot { get; set; }

    /// <summary>
    /// Path to the folder containg files we should return a random symlink to.
    /// </summary>
    public required string TargetFolder { get; set; }
    
    #endregion

    #region optional (as in "have default value") settings
    /// <summary>
    /// Amount of time between readlinks to be considered as parts of a single request
    /// to return the same target. Required because most real-world apps read the link more than once.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(100);
    
    #endregion

    #region debug output
    /// <summary>
    /// Whether to emit log data to be displayed by user app.
    /// </summary>
    public bool Verbose { get; set; } = false;
    
    /// <summary>
    /// An action to invoke to pass the log data back to the user.
    /// Console app should just write it.
    /// </summary>
    public Action<string>? LogAction { get; set; }

    #endregion

    #region random options
    /// <summary>
    /// Prevents returning the same target on two subsequent requests.
    /// </summary>
    public bool PreventRepeats { get; set; } = false;

    /// <summary>
    /// Instead of drawing randomly from the full file list, create a shuffled queue
    /// and draw targets from it. Ensures that every file is provided once before the queue is
    /// reshuffled and repeats start to occur.
    /// </summary>
    public bool UseQueue { get; set; } = false;

    #endregion
}
