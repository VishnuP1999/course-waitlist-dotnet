using System.Diagnostics.CodeAnalysis;

namespace CourseWaitlist.Core;

/// <summary>The single course offering this service manages.</summary>
/// <param name="RequiredPrerequisite">Course code a learner must have completed, or null when there is none.</param>
public sealed record OfferingConfig(string OfferingId, int Capacity, string? RequiredPrerequisite);

/// <summary>A learner and the course codes they have completed (compared case-insensitively).</summary>
public sealed record Learner(string Id, IReadOnlySet<string> CompletedCourses);

/// <summary>Read access to learner records. Real identity/records integrations are out of scope.</summary>
public interface ILearnerRecords
{
    bool TryGet(string learnerId, [NotNullWhen(true)] out Learner? learner);
}

// ---------------------------------------------------------------- results

public enum EnrollStatus
{
    Enrolled,
    Waitlisted,
    AlreadyEnrolled,
    AlreadyWaitlisted,
    NotEligible,
    UnknownLearner,
    InvalidRequest
}

public sealed record EnrollResult(EnrollStatus Status, string Message, int? WaitlistPosition = null);

public enum DropStatus
{
    /// <summary>An enrolled learner was dropped; a seat opened and the waitlist was processed.</summary>
    Dropped,

    /// <summary>A waitlisted learner left the queue. No seat was freed.</summary>
    RemovedFromWaitlist,

    /// <summary>The learner is neither enrolled nor waitlisted (includes repeated drops). Nothing changed.</summary>
    NotEnrolled,

    InvalidRequest
}

/// <param name="PromotedLearnerIds">Learners promoted into the freed seat(s), in promotion order.</param>
/// <param name="SkippedLearnerIds">Ineligible learners passed over; they keep their queue position.</param>
public sealed record DropResult(
    DropStatus Status,
    string Message,
    IReadOnlyList<string> PromotedLearnerIds,
    IReadOnlyList<string> SkippedLearnerIds);

public sealed record OfferingSnapshot(
    string OfferingId,
    int Capacity,
    int SeatsAvailable,
    IReadOnlyList<string> Enrolled,
    IReadOnlyList<string> Waitlist);
