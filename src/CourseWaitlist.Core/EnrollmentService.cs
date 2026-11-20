namespace CourseWaitlist.Core;

/// <summary>
/// Enrollment, drop and FIFO waitlist promotion for ONE course offering.
///
/// Invariants (all state changes happen under a single lock, so they hold between any two calls):
///   I1  enrolled count never exceeds capacity
///   I2  a learner appears at most once across enrolled + waitlist
///   I3  the waitlist keeps arrival order (FIFO); a skipped learner keeps their position
///   I4  an ineligible learner is never enrolled or promoted
///   I5  an open seat is always offered to the front of the waitlist before any new enrollee
///
/// Policy for an ineligible learner who reaches the front of the queue: SKIP and RETAIN
/// (they are passed over, keep their place, and are re-checked the next time a seat opens).
/// </summary>
public sealed class EnrollmentService
{
    private const int MaxLearnerIdLength = 64;

    // System.Threading.Lock (.NET 9+): a dedicated lock type, faster and clearer than locking on a plain object.
    private readonly Lock _gate = new();
    private readonly List<string> _enrolled = [];
    private readonly List<string> _waitlist = [];
    private readonly ILearnerRecords _records;
    private readonly OfferingConfig _config;

    public EnrollmentService(OfferingConfig config, ILearnerRecords records)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.Capacity, nameof(config.Capacity));
        ArgumentException.ThrowIfNullOrWhiteSpace(config.OfferingId, nameof(config.OfferingId));
        _config = config;
        _records = records;
    }

    public OfferingConfig Config => _config;

    /// <summary>
    /// Enrolls the learner if a seat is free, otherwise puts them on the waitlist.
    /// Learners who are unknown or fail the prerequisite are rejected and never stored.
    /// </summary>
    public EnrollResult Enroll(string? learnerId)
    {
        if (!TryNormalize(learnerId, out var id))
            return new EnrollResult(EnrollStatus.InvalidRequest, "learnerId is required (1-64 characters, no control characters).");

        lock (_gate)
        {
            if (_enrolled.Contains(id))
                return new EnrollResult(EnrollStatus.AlreadyEnrolled, $"Learner '{id}' is already enrolled.");

            var queued = _waitlist.IndexOf(id);
            if (queued >= 0)
                return new EnrollResult(EnrollStatus.AlreadyWaitlisted,
                    $"Learner '{id}' is already on the waitlist at position {queued + 1}.", queued + 1);

            if (!_records.TryGet(id, out var learner))
                return new EnrollResult(EnrollStatus.UnknownLearner, $"No learner record exists for '{id}'.");

            if (!IsEligible(learner))
                return new EnrollResult(EnrollStatus.NotEligible,
                    $"Learner '{id}' has not completed the prerequisite '{_config.RequiredPrerequisite}'.");

            // I5: earlier waitlisted learners get any open seat before a new enrollee can take it.
            FillOpenSeats([], []);

            if (_enrolled.Count < _config.Capacity)
            {
                _enrolled.Add(id);
                return new EnrollResult(EnrollStatus.Enrolled, $"Learner '{id}' is enrolled.");
            }

            _waitlist.Add(id);
            return new EnrollResult(EnrollStatus.Waitlisted,
                $"Course is full. Learner '{id}' joined the waitlist at position {_waitlist.Count}.", _waitlist.Count);
        }
    }

    /// <summary>
    /// Drops an enrolled learner (opening a seat and promoting the earliest eligible waitlisted learner)
    /// or removes a waitlisted learner from the queue. Anyone else, including a repeated drop, changes nothing.
    /// </summary>
    public DropResult Drop(string? learnerId)
    {
        if (!TryNormalize(learnerId, out var id))
            return new DropResult(DropStatus.InvalidRequest, "learnerId is required (1-64 characters, no control characters).",
                [], []);

        lock (_gate)
        {
            if (_enrolled.Remove(id))
            {
                List<string> promoted = [];
                List<string> skipped = [];
                FillOpenSeats(promoted, skipped);

                var message = promoted.Count > 0
                    ? $"Learner '{id}' dropped. '{promoted[0]}' was promoted from the waitlist."
                    : $"Learner '{id}' dropped. The seat is open (waitlist empty or no eligible learner).";
                return new DropResult(DropStatus.Dropped, message, promoted, skipped);
            }

            if (_waitlist.Remove(id))
                return new DropResult(DropStatus.RemovedFromWaitlist,
                    $"Learner '{id}' was removed from the waitlist.", [], []);

            return new DropResult(DropStatus.NotEnrolled,
                $"Learner '{id}' is not enrolled or waitlisted; nothing changed.", [], []);
        }
    }

    /// <summary>
    /// Re-evaluates the waitlist against the current learner records and fills any open seats.
    /// Call this after learner records change (for example a prerequisite is recorded as completed).
    /// </summary>
    public IReadOnlyList<string> FillOpenSeats()
    {
        lock (_gate)
        {
            List<string> promoted = [];
            FillOpenSeats(promoted, []);
            return promoted;
        }
    }

    /// <summary>A consistent copy of the current state.</summary>
    public OfferingSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new OfferingSnapshot(_config.OfferingId, _config.Capacity, _config.Capacity - _enrolled.Count,
                _enrolled.ToArray(), _waitlist.ToArray());
        }
    }

    // ---------------------------------------------------------------- internals (call only while holding _gate)

    /// <summary>
    /// Walks the waitlist from the front. Eligible learners are promoted while seats remain; ineligible
    /// learners are skipped but stay in place. The index only advances on a skip, because a promotion removes
    /// the element at that index.
    /// </summary>
    private void FillOpenSeats(List<string> promoted, List<string> skipped)
    {
        var index = 0;
        while (_enrolled.Count < _config.Capacity && index < _waitlist.Count)
        {
            var candidate = _waitlist[index];
            if (_records.TryGet(candidate, out var learner) && IsEligible(learner))
            {
                _waitlist.RemoveAt(index);
                _enrolled.Add(candidate);
                promoted.Add(candidate);
            }
            else
            {
                skipped.Add(candidate);
                index++;
            }
        }
    }

    private bool IsEligible(Learner learner) =>
        _config.RequiredPrerequisite is null || learner.CompletedCourses.Contains(_config.RequiredPrerequisite);

    private static bool TryNormalize(string? raw, out string id)
    {
        id = raw?.Trim() ?? string.Empty;
        return id.Length is > 0 and <= MaxLearnerIdLength && !id.Any(char.IsControl);
    }
}
