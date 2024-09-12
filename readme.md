# Ritsu FUSE
_(originally derived from "Random Target Symlink Filesystem in Userspace")_

**Ritsu FUSE** is a .NET app and/or library to create custom filesystems which contain symbolic links that change targets to a random file from a given folder after each "meaningful" read. "Meaningful" as in "the app probably does all the reads it needed in a small amount of time".

The main use is probably the extension of apps that only support loading single file for some purpose. By providing them with a Ritsu FUSE symlink, you can trick them to load different files every invocation.

Barebones demo with diagnostic log:
TODO terminals video

More concrete usage example:
TODO ffox video

## Requirements
.NET 8 runtime and an OS that supports FUSE (only tested on GNU/Linux, no idea if it works on anything else, let me know).

## Usage
```
$ Bnfour.RitsuFuse.ConsoleApp -?
Description:
  Ritsu FUSE console launcher. Ritsu FUSE is a library to create a custom file system that provides a symlink to a random file in a folder, changing after every "meaningful" read.

Usage:
  Bnfour.RitsuFuse.ConsoleApp <target folder> <file system root folder> [options]

Arguments:
  <target folder>            Folder with files to create a random symlink to. Must contain at least 2 files.
  <file system root folder>  Folder to host the file system. Must exist and be empty.

Options:
  --timeout <timeout>      Time (in milliseconds) between requests to continue returning the same target. Most apps read the link more than once. [default: 100]
  --verbose                Display diagnostic messages.
  --no-repeats             Prevents the same file being targeted twice in a row.
  --queue                  Use shuffled queue instead of full random. Returns each file in random order once before repeating.
  --link-name <link-name>  Name of the symlink in the file system folder. [default: ritsu]
  -v, --version            Display versions for this app and used library
  -?, -h, --help           Show help and usage information
```

### Arguments
The two required arguments are the two folders -- `target` with files to create links to, and `file system root` to contain the FUSE. When app is active, the `file system root` will contain a single symlink to a random file from `target` folder.

Both folders must exist and be accesible to the user, in addition:
- `file system root` folder must be empty
- `target` folder must contain at least two files to choose from.  
It is possible to add/remove files while the app is running, but please don't abuse it and leave at least two files in it at all times.

### Options
The rest of the options are optional and default to some value if unset. They can be used to fine-tune the app's behaviour.
#### Timeout
`--timeout <n>` where n > 1 sets the amount of time (in milliseconds, 100 by default) between link reads to be considered separate read, to which separate files are provided.

Most apps (except `readlink`) read the link a few times even when the "file" is opened once. You can use `--verbose` to find a value that works for you.

#### Verbosity
`--verbose` makes the app to provide some diagnostic information, the time between requests is probably the most useful; other data is more questionable.

#### Random options
_(that actually make it less random)_

`--no-repeats` makes sure that the app never returns the same file two separate reads in a row.

`--queue` tells the app to use a randomly shuffled queue of all files in the target folder instead of drawing randomly every time. So all the files will be returned once before the queue is reshuffled again and will be returned again -- each once in new random order.

Both of these options can be active at the same time!

#### Link name
`--link-name <name>` can be used to change symlink's default name, `ritsu`, to whatever else (which does not contain path separators and is neither `.` nor `..`).

### Global options
`-v` or `--version` displays the versions of both components of the app -- the console app, and the actual library.

`-?`, `-h` or `--help` shows help information listed at the start of this section.

### Termination
The created file system will be removed, leaving its root folder empty again when the app's process ends for any reason. It's also possible to use `umount(8)` like  
`$ umount <file system root folder>`  
to stop the app and also remove the created file system.

## Technical notes
TODO mtab entry, usage as library, also see next section

## Disclaimers/notes
TODO sort
- I have a vague understanding of FUSE internals and mono wrappers, the app just works for me.  
A lot of insight was gained from [this article](http://www.maastaar.net/fuse/linux/filesystem/c/2016/05/21/writing-a-simple-filesystem-using-fuse/). If you know how to improve the app's behaviour, you're welcome to send pulls.
- I cannot guarantee this app is free of error, remember the all-caps "use at your own risk" text from the license.
- Why Ritsu? It just came to my mind when I was expanding "RTS" as "random target symlink" to words to use as a name. I also like 「K-ON!」, maybe you noticed.
- [System.Commandline](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) is really cool. I always missed [python's argparse](https://docs.python.org/3/library/argparse.html) in .NET world.

## License
MIT (as usual), see [LICENSE](LICENSE) for the full text.
