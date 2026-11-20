# Limitations and follow-up risks (within scope)

1. **No persistence.** State is in memory and is lost on restart. A real version needs a database transaction around enroll/drop
   (row lock or optimistic concurrency on the offering) so the same invariants hold across restarts and multiple instances. The single
   in-process lock does **not** protect a scaled-out deployment.
2. **Single instance only.** Two API instances would each have their own state and lock.
3. **Learner records are a stub.** `PUT /learners/{id}` is an unauthenticated stand-in for a student-records source; anyone who can call
   it can change eligibility. Real identity and authorization were out of scope.
4. **Eligibility is a single prerequisite course.** No grades, co-requisites, or "any of" rules. Adding them means changing `IsEligible`
   (and the brief asks for no configurable policy engine).
5. **Ineligible waitlisted learners only arise when records change.** Because ineligible learners cannot join, a skipped learner exists only
   if their record changed after joining. The skip-and-retain path is real and tested, but rare in practice.
6. **Lookups under the lock.** Record lookups run inside the critical section. Fine in memory; a slow remote source would need prefetching.
7. **O(n) list operations.** Membership and position use list scans. Fine for one offering's capacity and queue; a very long waitlist would
   want an indexed structure (kept out to avoid order and membership drifting apart).
8. **Case-sensitive learner ids in the service and API.** `L101` and `l101` are different learners there (the console accepts either and maps it to the table's ID). A real system should use a canonical id from the identity provider.
9. **No notifications, audit trail or waitlist time limits.** A promoted learner is not notified and has no deadline to accept.
10. **Test runner is a shim.** The sandbox had no NuGet access, so tests run on an xUnit-compatible shim. Switch to real xUnit (steps in `docs/API_GUIDE.md`)
    if the team's CI expects `dotnet test`.
11. **The console is a demonstration front end.** Its sample learners, course names and the demo's fixed configuration (NET301, capacity 2,
    prerequisite CSH101) are hard-coded. Learner names exist only in the console; the service and API work with IDs. The console and the API do not share state; each process has its own in-memory store.
12. **The `Show-Body` PowerShell helper in `docs/API_GUIDE.md` has not itself been run.** It combines the two approaches tried by hand on Windows
    PowerShell: `$_.ErrorDetails.Message`, which printed nothing for the 422 response there, and reading the response stream, which
    showed the body. It has not been tried on PowerShell 7 either.
13. **One equivalent mutant survived (M11 in the verification record).** Changing `Drop` so that a request for someone who is not on the
    course also fills open seats did not fail any test. That behaviour is harmless (it can only help and never breaks an invariant),
    so it is deliberately not treated as a test gap.
