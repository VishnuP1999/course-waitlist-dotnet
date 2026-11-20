using CourseWaitlist.Core;
using Xunit;

namespace CourseWaitlist.Tests;

/// <summary>
/// The service guards all state with one lock. These tests hammer it from many threads and check that the
/// capacity and uniqueness invariants still hold and that results are consistent with the final state.
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public void Many_learners_enrolling_at_once_never_exceed_capacity()
    {
        const int capacity = 5;
        const int learners = 400;
        var f = new Fixture(capacity);
        var ids = f.AddMany(learners);
        var results = new EnrollResult[learners];

        Parallel.For(0, learners, new ParallelOptions { MaxDegreeOfParallelism = 32 }, i => results[i] = f.Service.Enroll(ids[i]));

        Assert.Equal(capacity, results.Count(r => r.Status == EnrollStatus.Enrolled));
        Assert.Equal(learners - capacity, results.Count(r => r.Status == EnrollStatus.Waitlisted));
        var s = f.Service.Snapshot();
        Assert.Equal(capacity, s.Enrolled.Count);
        Assert.Equal(learners - capacity, s.Waitlist.Count);
        // waitlist positions handed out are unique and contiguous
        Assert.Equal(Enumerable.Range(1, learners - capacity),
            results.Where(r => r.Status == EnrollStatus.Waitlisted).Select(r => r.WaitlistPosition!.Value).OrderBy(p => p));
        f.AssertInvariants();
    }

    [Fact]
    public void The_same_learner_requested_concurrently_is_recorded_exactly_once()
    {
        var f = new Fixture(capacity: 3);
        f.Add("ana");
        var results = new EnrollResult[100];

        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 32 }, i => results[i] = f.Service.Enroll("ana"));

        Assert.Equal(1, results.Count(r => r.Status == EnrollStatus.Enrolled));
        Assert.Equal(99, results.Count(r => r.Status == EnrollStatus.AlreadyEnrolled));
        Assert.Equal(new[] { "ana" }, f.Service.Snapshot().Enrolled);
    }

    [Fact]
    public void Concurrent_drops_of_the_same_learner_free_exactly_one_seat_and_promote_exactly_one()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(4);
        foreach (var id in new[] { "L1", "L2", "L3", "L4" }) f.Service.Enroll(id);
        var results = new DropResult[60];

        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 32 }, i => results[i] = f.Service.Drop("L1"));

        Assert.Equal(1, results.Count(r => r.Status == DropStatus.Dropped));
        Assert.Equal(59, results.Count(r => r.Status == DropStatus.NotEnrolled));
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "L2" }, s.Enrolled);
        Assert.Equal(new[] { "L3", "L4" }, s.Waitlist);
        f.AssertInvariants();
    }

    /// <summary>Widens the check-then-act window so an unlocked implementation fails reliably.</summary>
    private sealed class SlowRecords : ILearnerRecords
    {
        private readonly ILearnerRecords _inner;
        public SlowRecords(ILearnerRecords inner) => _inner = inner;

        public bool TryGet(string learnerId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Learner? learner)
        {
            Thread.Sleep(2);
            return _inner.TryGet(learnerId, out learner);
        }
    }

    [Fact]
    public void Concurrent_promotion_attempts_into_one_open_seat_promote_exactly_one_learner()
    {
        var f = new Fixture(capacity: 1, decorate: r => new SlowRecords(r));
        f.Add("L1");
        for (var i = 2; i <= 30; i++) f.Add($"L{i}", hasPrerequisite: true);
        f.Service.Enroll("L1");
        for (var i = 2; i <= 30; i++) f.Service.Enroll($"L{i}");           // 29 waitlisted

        for (var i = 2; i <= 30; i++) f.Add($"L{i}", hasPrerequisite: false); // all become ineligible
        f.Service.Drop("L1");                                                // seat opens, everyone skipped
        Assert.Equal(1, f.Service.Snapshot().SeatsAvailable);

        for (var i = 2; i <= 30; i++) f.Add($"L{i}", hasPrerequisite: true);  // all eligible again

        var promotedCounts = new int[16];
        Parallel.For(0, promotedCounts.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 },
            i => promotedCounts[i] = f.Service.FillOpenSeats().Count);

        Assert.Equal(1, promotedCounts.Sum());
        Assert.Equal(new[] { "L2" }, f.Service.Snapshot().Enrolled);   // FIFO winner
        f.AssertInvariants();
    }

    [Fact]
    public void Mixed_concurrent_enrolls_and_drops_preserve_all_invariants()
    {
        const int capacity = 4;
        var f = new Fixture(capacity);
        var ids = f.AddMany(40);

        Parallel.For(0, 4000, new ParallelOptions { MaxDegreeOfParallelism = 32 }, i =>
        {
            var rng = new Random(i);
            var id = ids[rng.Next(ids.Count)];
            if (rng.Next(2) == 0) f.Service.Enroll(id); else f.Service.Drop(id);

            // invariants must hold at every observable moment, not only at the end
            var s = f.Service.Snapshot();
            if (s.Enrolled.Count > capacity) throw new InvalidOperationException("capacity exceeded");
            if (s.Enrolled.Intersect(s.Waitlist).Any()) throw new InvalidOperationException("learner both enrolled and waitlisted");
        });

        f.AssertInvariants();
        var final = f.Service.Snapshot();
        // work conservation: with an eligible waitlist, a seat is never left open
        Assert.True(final.Waitlist.Count == 0 || final.SeatsAvailable == 0, "seat left open while eligible learners are waiting");
    }
}
