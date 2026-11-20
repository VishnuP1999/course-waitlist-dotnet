using CourseWaitlist.Core;
using Xunit;

namespace CourseWaitlist.Tests;

/// <summary>
/// Builds a service for a course "NET301" requiring "CSH101". Learners named "ok-*" have the prerequisite,
/// anything registered with <see cref="Add"/> can be given or denied it explicitly.
/// </summary>
public sealed class Fixture
{
    public const string Prerequisite = "CSH101";

    public InMemoryLearnerRecords Records { get; } = new();
    public EnrollmentService Service { get; }
    public int Capacity { get; }

    public Fixture(int capacity = 2, string? prerequisite = Prerequisite, Func<ILearnerRecords, ILearnerRecords>? decorate = null)
    {
        Capacity = capacity;
        Service = new EnrollmentService(new OfferingConfig("NET301", capacity, prerequisite), decorate?.Invoke(Records) ?? Records);
    }

    /// <summary>Registers a learner who has (default) or lacks the prerequisite.</summary>
    public string Add(string id, bool hasPrerequisite = true)
    {
        Records.Upsert(id, hasPrerequisite ? new[] { Prerequisite } : Array.Empty<string>());
        return id;
    }

    /// <summary>Registers learners "L1".."Ln" with the prerequisite.</summary>
    public IReadOnlyList<string> AddMany(int count)
    {
        var ids = Enumerable.Range(1, count).Select(i => $"L{i}").ToList();
        foreach (var id in ids) Add(id);
        return ids;
    }

    /// <summary>Asserts the invariants I1 and I2 on the current state.</summary>
    public void AssertInvariants()
    {
        var s = Service.Snapshot();
        Assert.True(s.Enrolled.Count <= s.Capacity, $"I1 violated: {s.Enrolled.Count} enrolled for capacity {s.Capacity}");
        Assert.Equal(s.Capacity - s.Enrolled.Count, s.SeatsAvailable);
        Assert.Equal(s.Enrolled.Count, s.Enrolled.Distinct().Count());
        Assert.Equal(s.Waitlist.Count, s.Waitlist.Distinct().Count());
        Assert.True(!s.Enrolled.Intersect(s.Waitlist).Any(), "I2 violated: a learner is both enrolled and waitlisted");
    }
}
