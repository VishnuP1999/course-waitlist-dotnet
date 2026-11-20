using CourseWaitlist.Core;
using Xunit;

namespace CourseWaitlist.Tests;

/// <summary>Acceptance criteria 1 and 2: capacity, full, duplicates.</summary>
public class EnrollmentTests
{
    [Fact]
    public void Enrolls_when_capacity_remains_and_prerequisite_is_met()
    {
        var f = new Fixture(capacity: 2);
        f.Add("ana");

        var result = f.Service.Enroll("ana");

        Assert.Equal(EnrollStatus.Enrolled, result.Status);
        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "ana" }, s.Enrolled);
        Assert.Equal(1, s.SeatsAvailable);
        Assert.Empty(s.Waitlist);
    }

    [Fact]
    public void Fills_to_capacity_then_eligible_learners_join_the_waitlist_in_order()
    {
        var f = new Fixture(capacity: 2);
        f.AddMany(4);

        Assert.Equal(EnrollStatus.Enrolled, f.Service.Enroll("L1").Status);
        Assert.Equal(EnrollStatus.Enrolled, f.Service.Enroll("L2").Status);

        var third = f.Service.Enroll("L3");
        var fourth = f.Service.Enroll("L4");

        Assert.Equal(EnrollStatus.Waitlisted, third.Status);
        Assert.Equal(1, third.WaitlistPosition);
        Assert.Equal(EnrollStatus.Waitlisted, fourth.Status);
        Assert.Equal(2, fourth.WaitlistPosition);

        var s = f.Service.Snapshot();
        Assert.Equal(new[] { "L1", "L2" }, s.Enrolled);
        Assert.Equal(new[] { "L3", "L4" }, s.Waitlist);
        Assert.Equal(0, s.SeatsAvailable);
        f.AssertInvariants();
    }

    [Fact]
    public void Duplicate_enroll_of_an_enrolled_learner_changes_nothing()
    {
        var f = new Fixture(capacity: 2);
        f.AddMany(1);
        f.Service.Enroll("L1");

        var again = f.Service.Enroll("L1");

        Assert.Equal(EnrollStatus.AlreadyEnrolled, again.Status);
        Assert.Equal(new[] { "L1" }, f.Service.Snapshot().Enrolled);
        f.AssertInvariants();
    }

    [Fact]
    public void Duplicate_enroll_of_a_waitlisted_learner_keeps_a_single_record_and_position()
    {
        var f = new Fixture(capacity: 1);
        f.AddMany(3);
        f.Service.Enroll("L1");
        f.Service.Enroll("L2");
        f.Service.Enroll("L3");

        var again = f.Service.Enroll("L2");

        Assert.Equal(EnrollStatus.AlreadyWaitlisted, again.Status);
        Assert.Equal(1, again.WaitlistPosition);
        Assert.Equal(new[] { "L2", "L3" }, f.Service.Snapshot().Waitlist);
        f.AssertInvariants();
    }

    [Fact]
    public void Duplicate_requests_with_different_spacing_are_the_same_learner()
    {
        var f = new Fixture(capacity: 2);
        f.Add("ana");
        f.Service.Enroll("ana");

        Assert.Equal(EnrollStatus.AlreadyEnrolled, f.Service.Enroll("  ana  ").Status);
        Assert.Equal(1, f.Service.Snapshot().Enrolled.Count);
    }

    [Fact]
    public void Learner_ids_are_case_sensitive()
    {
        var f = new Fixture(capacity: 2);
        f.Add("Ana");
        f.Add("ana");

        Assert.Equal(EnrollStatus.Enrolled, f.Service.Enroll("Ana").Status);
        Assert.Equal(EnrollStatus.Enrolled, f.Service.Enroll("ana").Status);
        Assert.Equal(2, f.Service.Snapshot().Enrolled.Count);
    }

    [Fact]
    public void Enrollment_without_a_prerequisite_requirement_accepts_everyone_registered()
    {
        var f = new Fixture(capacity: 1, prerequisite: null);
        f.Add("newbie", hasPrerequisite: false);

        Assert.Equal(EnrollStatus.Enrolled, f.Service.Enroll("newbie").Status);
    }

    [Fact]
    public void Constructor_rejects_invalid_configuration()
    {
        var records = new InMemoryLearnerRecords();
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnrollmentService(new OfferingConfig("NET301", 0, null), records));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnrollmentService(new OfferingConfig("NET301", -3, null), records));
        Assert.Throws<ArgumentException>(() => new EnrollmentService(new OfferingConfig(" ", 5, null), records));
    }
}

/// <summary>Acceptance criterion 4 (enrollment side): prerequisite and record failures.</summary>
public class EligibilityTests
{
    [Fact]
    public void Learner_without_the_prerequisite_is_rejected_even_when_seats_are_open()
    {
        var f = new Fixture(capacity: 2);
        f.Add("zed", hasPrerequisite: false);

        var result = f.Service.Enroll("zed");

        Assert.Equal(EnrollStatus.NotEligible, result.Status);
        Assert.Contains("CSH101", result.Message);
        var s = f.Service.Snapshot();
        Assert.Empty(s.Enrolled);
        Assert.Empty(s.Waitlist);
    }

    [Fact]
    public void Ineligible_learner_does_not_join_the_waitlist_when_the_course_is_full()
    {
        var f = new Fixture(capacity: 1);
        f.Add("ok");
        f.Add("zed", hasPrerequisite: false);
        f.Service.Enroll("ok");

        var result = f.Service.Enroll("zed");

        Assert.Equal(EnrollStatus.NotEligible, result.Status);
        Assert.Empty(f.Service.Snapshot().Waitlist);
    }

    [Fact]
    public void Prerequisite_comparison_ignores_case()
    {
        var f = new Fixture(capacity: 1);
        f.Records.Upsert("ana", new[] { "csh101" });

        Assert.Equal(EnrollStatus.Enrolled, f.Service.Enroll("ana").Status);
    }

    [Fact]
    public void Unknown_learner_is_rejected_and_not_stored()
    {
        var f = new Fixture(capacity: 2);

        var result = f.Service.Enroll("ghost");

        Assert.Equal(EnrollStatus.UnknownLearner, result.Status);
        var s = f.Service.Snapshot();
        Assert.Empty(s.Enrolled);
        Assert.Empty(s.Waitlist);
    }
}

/// <summary>Acceptance criterion 5 (enroll side): invalid requests leave state untouched.</summary>
public class InvalidRequestTests
{
    [Fact]
    public void Blank_null_overlong_and_control_character_ids_are_invalid_and_change_nothing()
    {
        var f = new Fixture(capacity: 2);
        f.AddMany(1);
        f.Service.Enroll("L1");
        var before = f.Service.Snapshot();

        foreach (var bad in new string?[] { null, "", "   ", new string('x', 65), "bad\nid", "tab\tid" })
        {
            Assert.Equal(EnrollStatus.InvalidRequest, f.Service.Enroll(bad).Status);
            Assert.Equal(DropStatus.InvalidRequest, f.Service.Drop(bad).Status);
        }

        var after = f.Service.Snapshot();
        Assert.Equal(before.Enrolled, after.Enrolled);
        Assert.Equal(before.Waitlist, after.Waitlist);
        f.AssertInvariants();
    }
}
