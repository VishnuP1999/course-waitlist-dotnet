using CourseWaitlist.Cli;
using CourseWaitlist.Core;

// Usage:
//   dotnet run --project src/CourseWaitlist.Cli                 interactive menu with sample learners
//   dotnet run --project src/CourseWaitlist.Cli -- --demo       run the guided scenario and exit (0 = all checks matched)
// Options: --capacity <n>   --prerequisite <code|none>

var demo = false;
var capacity = 2;
string? prerequisite = CliApp.PrerequisiteCode;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--demo":
            demo = true;
            break;
        case "--capacity" when i + 1 < args.Length && int.TryParse(args[i + 1], out var c) && c > 0:
            capacity = c;
            i++;
            break;
        case "--prerequisite" when i + 1 < args.Length:
            prerequisite = args[i + 1].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[i + 1];
            i++;
            break;
        case "--help" or "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown or invalid argument '{args[i]}'.");
            PrintUsage();
            return 2;
    }
}

var app = new CliApp(Console.In, Console.Out, new OfferingConfig(CliApp.OfferingCode, capacity, prerequisite));
return demo ? app.RunDemo() : app.RunInteractive();

static void PrintUsage() => Console.WriteLine("""
    Course Waitlist console: NET301 ASP.NET Core Web API Development

      (no arguments)            interactive menu with sample learners preloaded
      --demo                    run the guided scenario and exit (exit code 0 when every check matches)
      --capacity <n>            seats in the offering (default 2)
      --prerequisite <code>     required course code (default CSH101 = C# Fundamentals; "none" for no requirement)
    """);
