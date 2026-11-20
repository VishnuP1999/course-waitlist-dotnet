using CourseWaitlist.Cli;
using CourseWaitlist.Core;
using Xunit;

namespace CourseWaitlist.Tests;

/// <summary>Drives the interactive console with scripted input, the same way a reviewer would type.</summary>
public class CliTests
{
    private static (int ExitCode, string Output) Run(string input, Func<CliApp, int> mode)
    {
        var output = new StringWriter();
        var app = new CliApp(new StringReader(input), output, new OfferingConfig(CliApp.OfferingCode, 2, CliApp.PrerequisiteCode));
        var exit = mode(app);
        return (exit, output.ToString());
    }

    [Fact]
    public void Guided_demo_runs_every_step_as_expected()
    {
        var (exit, output) = Run("", app => app.RunDemo());

        Assert.Equal(0, exit);
        Assert.Contains("Demo result: 16/16 checks matched", output);
        Assert.False(output.Contains("MISMATCH"), output);
    }

    [Fact]
    public void Start_screen_shows_the_course_prerequisite_and_a_learner_table()
    {
        var (_, output) = Run("0\n", app => app.RunInteractive());

        Assert.Contains("Course       : NET301 - ASP.NET Core Web API Development", output);
        Assert.Contains("Prerequisite : CSH101 - C# Fundamentals", output);
        Assert.Contains("| # | ID   | Name          | Completed courses | Has CSH101? | Status       |", output);
        Assert.Contains("| 1 | L101 | Ramesh Kumar  | CSH101, SQL101    | Yes         | Not enrolled |", output);
        Assert.Contains("| 7 | L107 | Rakesh Verma  | JS101             | No          | Not enrolled |", output);
    }

    [Fact]
    public void Row_numbers_select_learners_for_enroll_waitlist_and_drop()
    {
        // enroll rows 1, 2, 3 (capacity 2), then drop row 1
        var (exit, output) = Run("1\n1\n1\n2\n1\n3\n2\n1\n3\n0\n", app => app.RunInteractive());

        Assert.Equal(0, exit);
        Assert.Contains("[Enrolled] Learner Ramesh Kumar (L101) is enrolled.", output);
        Assert.Contains("[Enrolled] Learner Priya Sharma (L102) is enrolled.", output);
        Assert.Contains("[Waitlisted] Course is full. Learner Arjun Reddy (L103) joined the waitlist at position 1.", output);
        Assert.Contains("promoted from the waitlist: Arjun Reddy", output);
        Assert.Contains("| 3 | L103 | Arjun Reddy   | CSH101, JS101     | Yes         | Enrolled     |", output);
    }

    [Fact]
    public void Ineligible_out_of_range_unknown_and_ambiguous_input_are_reported_clearly()
    {
        var (_, output) = Run("1\n7\n1\n99\n1\nghost\n1\nar\n2\n5\n0\n", app => app.RunInteractive());

        Assert.Contains("[NotEligible] Learner Rakesh Verma (L107) has not completed the prerequisite 'CSH101 - C# Fundamentals'.", output);
        Assert.Contains("There is no row #99. Pick a number from 1 to 8.", output);
        Assert.Contains("[UnknownLearner]", output);
        Assert.Contains("'ar' matches", output);   // Arjun Reddy, Sarah Johnson, ... are all possible
        Assert.Contains("[NotEnrolled] Learner John Miller (L105) is not enrolled or waitlisted; nothing changed.", output);
    }

    [Fact]
    public void Typing_a_number_never_creates_or_enrolls_a_learner_with_that_id()
    {
        // The confusion this guards against: earlier versions let you create a learner whose ID was "1".
        var (_, output) = Run("4\nKavya Nair\nCSH101, js101\n1\n9\n0\n", app => app.RunInteractive());

        Assert.Contains("[Added] Kavya Nair (L109) - completed: CSH101, JS101 - prerequisite CSH101 met: Yes", output);
        Assert.Contains("[Enrolled] Learner Kavya Nair (L109) is enrolled.", output);
        Assert.False(output.Contains("Learner '1'"), "a typed number must never become a learner id");
        Assert.False(output.Contains("Learner '9'"), "a typed number must never become a learner id");
    }

    [Fact]
    public void Updating_completed_courses_changes_eligibility()
    {
        // Rakesh (row 7) is ineligible, completes CSH101, then enrolls; names and IDs are case-insensitive
        var (_, output) = Run("1\nrakesh\n5\n7\ncsh101, JS101\n1\nl107\n0\n", app => app.RunInteractive());

        Assert.Contains("[NotEligible] Learner Rakesh Verma (L107)", output);
        Assert.Contains("[Updated] Rakesh Verma (L107) - completed: CSH101, JS101 - prerequisite CSH101 met: Yes", output);
        Assert.Contains("[Enrolled] Learner Rakesh Verma (L107) is enrolled.", output);
    }

    [Fact]
    public void Unknown_option_and_closed_input_exit_cleanly()
    {
        var (exit, output) = Run("9\n", app => app.RunInteractive());   // input ends without choosing Exit

        Assert.Equal(0, exit);
        Assert.Contains("Unknown option '9'", output);
    }
}
