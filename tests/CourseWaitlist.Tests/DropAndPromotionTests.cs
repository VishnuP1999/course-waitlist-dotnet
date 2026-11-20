using CourseWaitlist.Core;
using Xunit;

namespace CourseWaitlist.Tests;

/// <summary>Acceptance criteria 3, 4 and 5: drop, FIFO promotion, skip-and-retain policy, repeated drops.</summary>
public class DropAndPromotionTests
{
    [Fact]
    public void Drop_with_empty_waitlist_just_opens_the_seat()
    {
        var f = new Fixture(capacity: 2);
        f.AddMany(1);
        f.Service.Enroll("L1");

        var result = f.Service.Drop("L1");

        Assert.Equal(DropStatus.Dropped, result.Status);
        Assert.Empty(result.PromotedLearnerIds);
        var s = f.Service.Snapshot();
        Assert.Empty(s.Enrolled);
        Assert.Equal(2, s.SeatsAvailable);
    }

    [Fact]
    public void Drop_promotes_the_earliest_waitlisted_learner_first_in_first_out()
    {
        var f = new Fixture(capacity: 2);
        f.AddMany(5);
        foreach (var id in new[] { "L1", "L2", "L3", "L4", "L5" }) f.Service.Enroll(id); // L3, L4, L5 waitlisted

        var drop = f.Service.Drop("L1");

        Assert.Equal(DropStatus.Dropped, drop.Status);
        Assert.Equal(new[] { "L3" }, drop.PromotedLearnerIds);
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "L2", "L3" }, s.Enrolled);
        Assert.Equal(new[] { "L4", "L5" }, s.Waitlist);
        f.AssertInvariants();
    }

    [Fact]
    public void Successive_drops_promote_in_arrival_order()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(4);
        foreach (var id in new[] { "L1", "L2", "L3", "L4" }) f.Service.Enroll(id);

        Assert.Equal(new[] { "L2" }, f.Service.Drop("L1").PromotedLearnerIds);
        Assert.Equal(new[] { "L3" }, f.Service.Drop("L2").PromotedLearnerIds);
        Assert.Equal(new[] { "L4" }, f.Service.Drop("L3").PromotedLearnerIds);
        Assert.Equal(new[] { "L4" }, f.Service.Snapshot().Enrolled);
        Assert.Empty(f.Service.Snapshot().Waitlist);
    }

    [Fact]
    public void Ineligible_learner_at_the_front_is_skipped_keeps_their_place_and_the_next_eligible_is_promoted()
    {
        var f = new Fixture(capacity: 1);
        f.Add("L1");
        f.Add("front");
        f.Add("second");
        f.Service.Enroll("L1");
        f.Service.Enroll("front");   // waitlist position 1
        f.Service.Enroll("second");  // waitlist position 2

        // front loses the prerequisite record while waiting (records changed after they joined)
        f.Add("front", hasPrerequisite: false);

        var drop = f.Service.Drop("L1");

        Assert.Equal(new[] { "second" }, drop.PromotedLearnerIds);
        Assert.Equal(new[] { "front" }, drop.SkippedLearnerIds);
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "second" }, s.Enrolled);
        Assert.Equal(new[] { "front" }, s.Waitlist);   // retained, not removed
        Assert.True(!s.Enrolled.Contains("front"), "an ineligible learner must never be enrolled");
        f.AssertInvariants();
    }

    [Fact]
    public void Skipped_learner_is_promoted_on_a_later_seat_once_eligible_again_and_before_anyone_behind_them()
    {
        var f = new Fixture(capacity: 1);
        f.Add("L1");
        f.Add("front");
        f.Add("behind");
        f.Service.Enroll("L1");
        f.Service.Enroll("front");
        f.Service.Enroll("behind");
        f.Add("front", hasPrerequisite: false);

        f.Service.Drop("L1");                       // behind promoted, front skipped but retained
        f.Add("front", hasPrerequisite: true);      // front completes the prerequisite
        f.Add("late");
        f.Service.Enroll("late");                   // late joins the back of the queue
        var drop = f.Service.Drop("behind");        // next seat

        Assert.Equal(new[] { "front" }, drop.PromotedLearnerIds);   // original place honoured
        Assert.Equal(new[] { "late" }, f.Service.Snapshot().Waitlist);
        f.AssertInvariants();
    }

    [Fact]
    public void When_every_waitlisted_learner_is_ineligible_the_seat_stays_open_and_everyone_keeps_their_place()
    {
        var f = new Fixture(capacity: 1);
        f.Add("L1");
        f.Add("a");
        f.Add("b");
        f.Service.Enroll("L1");
        f.Service.Enroll("a");
        f.Service.Enroll("b");
        f.Add("a", hasPrerequisite: false);
        f.Add("b", hasPrerequisite: false);

        var drop = f.Service.Drop("L1");

        Assert.Empty(drop.PromotedLearnerIds);
        Assert.Equal(new[] { "a", "b" }, drop.SkippedLearnerIds);
        var s = f.Service.Snapshot();
        Assert.Empty(s.Enrolled);
        Assert.Equal(1, s.SeatsAvailable);
        Assert.Equal(new[] { "a", "b" }, s.Waitlist);
    }

    [Fact]
    public void A_new_enrollee_cannot_jump_a_waitlisted_learner_who_has_become_eligible()
    {
        var f = new Fixture(capacity: 1);
        f.Add("L1");
        f.Add("front");
        f.Add("newcomer");
        f.Service.Enroll("L1");
        f.Service.Enroll("front");
        f.Add("front", hasPrerequisite: false);
        f.Service.Drop("L1");                       // front skipped, seat open
        f.Add("front", hasPrerequisite: true);      // eligible again, but nobody triggered a promotion yet

        var result = f.Service.Enroll("newcomer");  // must not take the seat ahead of front (I5)

        Assert.Equal(EnrollStatus.Waitlisted, result.Status);
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "front" }, s.Enrolled);
        Assert.Equal(new[] { "newcomer" }, s.Waitlist);
        f.AssertInvariants();
    }

    [Fact]
    public void FillOpenSeats_promotes_after_records_change()
    {
        var f = new Fixture(capacity: 1);
        f.Add("L1");
        f.Add("front");
        f.Service.Enroll("L1");
        f.Service.Enroll("front");
        f.Add("front", hasPrerequisite: false);
        f.Service.Drop("L1");
        f.Add("front", hasPrerequisite: true);

        var promoted = f.Service.FillOpenSeats();

        Assert.Equal(new[] { "front" }, promoted);
        Assert.Equal(new[] { "front" }, f.Service.Snapshot().Enrolled);
    }

    [Fact]
    public void Repeated_drop_is_reported_clearly_and_does_not_free_a_second_seat_or_promote_twice()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(3);
        foreach (var id in new[] { "L1", "L2", "L3" }) f.Service.Enroll(id);

        var first = f.Service.Drop("L1");
        var second = f.Service.Drop("L1");

        Assert.Equal(DropStatus.Dropped, first.Status);
        Assert.Equal(DropStatus.NotEnrolled, second.Status);
        Assert.Empty(second.PromotedLearnerIds);
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "L2" }, s.Enrolled);   // still exactly one promotion
        Assert.Equal(new[] { "L3" }, s.Waitlist);
        f.AssertInvariants();
    }

    [Fact]
    public void Dropping_someone_who_never_enrolled_changes_nothing()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(2);
        f.Service.Enroll("L1");
        f.Service.Enroll("L2");
        var before = f.Service.Snapshot();

        var result = f.Service.Drop("nobody");

        Assert.Equal(DropStatus.NotEnrolled, result.Status);
        var after = f.Service.Snapshot();
        Assert.Equal(before.Enrolled, after.Enrolled);
        Assert.Equal(before.Waitlist, after.Waitlist);
    }

    [Fact]
    public void Dropping_a_waitlisted_learner_removes_them_without_freeing_a_seat()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(3);
        foreach (var id in new[] { "L1", "L2", "L3" }) f.Service.Enroll(id);

        var result = f.Service.Drop("L2");

        Assert.Equal(DropStatus.RemovedFromWaitlist, result.Status);
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "L1" }, s.Enrolled);
        Assert.Equal(new[] { "L3" }, s.Waitlist);
        Assert.Equal(0, s.SeatsAvailable);
    }

    [Fact]
    public void A_learner_who_drops_and_re_enrolls_goes_to_the_back_of_the_queue()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(3);
        foreach (var id in new[] { "L1", "L2", "L3" }) f.Service.Enroll(id);

        f.Service.Drop("L1");                       // L2 promoted
        var again = f.Service.Enroll("L1");

        Assert.Equal(EnrollStatus.Waitlisted, again.Status);
        Assert.Equal(new[] { "L3", "L1" }, f.Service.Snapshot().Waitlist);
    }

    [Fact]
    public void Randomised_operation_sequences_keep_every_invariant_and_match_a_simple_reference_model()
    {
        var rng = new Random(20261119);
        var f = new Fixture(capacity: 3);
        var ids = f.AddMany(12);

        // reference model: only eligible learners (all of them here), strict FIFO
        var enrolled = new List<string>();
        var waitlist = new List<string>();

        for (var step = 0; step < 3000; step++)
        {
            var id = ids[rng.Next(ids.Count)];
            if (rng.Next(2) == 0)
            {
                f.Service.Enroll(id);
                if (!enrolled.Contains(id) && !waitlist.Contains(id))
                {
                    if (enrolled.Count < 3) enrolled.Add(id); else waitlist.Add(id);
                }
            }
            else
            {
                f.Service.Drop(id);
                if (enrolled.Remove(id))
                {
                    if (waitlist.Count > 0) { enrolled.Add(waitlist[0]); waitlist.RemoveAt(0); }
                }
                else waitlist.Remove(id);
            }

            f.AssertInvariants();
            var s = f.Service.Snapshot();
            Assert.Equal(enrolled, s.Enrolled);
            Assert.Equal(waitlist, s.Waitlist);
        }
    }
}
