# AI Prompt and Use Record

**Tool:** Claude (Anthropic), used as an engineering assistant through a coding-agent session that could create files, run builds and run tests.
Some conversations were by voice; those are summarised, not quoted.
**Rule followed:** no credentials, secrets or personal data were put into any prompt or this record.

Sections 1-4 record what the AI did and the evidence for it. Section 5 is my own review and verification.

## 1. Prompts used

| # | Prompt (verbatim where typed, summarised where spoken) | Why |
|---|---|---|
| P1 | "check and tell me the mermaid diagram on this. and tell me what efforts are required." (assessment brief attached) | Understand the task; get a decomposition, diagrams and an effort estimate before building |
| P2 | "make this with C# and .NET concepts. let me know the questions if any." | Build the scoped slice in C# / .NET |
| P3 | "check for the new one have we followed the same set of roles, responsibilities etc" (a colleague's agent-instructions file from a different assignment attached) | Check the work against a stricter engineering checklist: roles, verification, definition of done |
| P4 | "does this document clarifies things?" (the assessment PDF attached) | Confirm the brief; look for a starter project or rubric |
| P5 | "change the files to .net 10 and supportable C# content" | Retarget to a supported .NET version |
| P6 | Voice, while verifying on my machine: how to run the API, why `promotedLearnerIds` is empty, why the `NotEligible` message is not shown for a 422 | Understand behaviour while testing by hand |
| P7 | Voice, then typed: "interactive console changes. and also write these things in step 5. that I suggested this like that" | Make the work easy for reviewers to run; record my own review |
| P8 | Voice, then typed (after trying the first console): "this is the console. its not user friendly. if its in memory then It should show me the names (real names few Indian names and few Americans name) and in table structure show me have they done pre-req change the course name to something related to .NET Like Javascript preq for React / Angular course similar to this. and also check for why 1 is enrolled even though one was not present." | Fix the usability problems I found; explain a result that looked wrong |

## 2. What the AI contributed and how each piece was handled

| # | AI output / suggestion | Decision | Files / decisions it informed | How verified |
|---|---|---|---|---|
| A1 | **Task decomposition and effort estimate** (6-8 h: read starter 0.5-1, core 1.5-2, tests 1.5-2, spec 1, AI record 0.5-1, README 0.5, review 0.5-1) | Accepted as a planning aid | Work order for this submission | Compared with the brief's 7-8 h timebox |
| A2 | **Assumption: ineligible learner at the front is skipped and retained** (alternatives weighed: remove from queue; block the queue) | Accepted | `EnrollmentService.FillOpenSeats`, `docs/DESIGN.md` §2 and §8 | Tests: `Ineligible_learner_at_the_front_is_skipped...`, `Skipped_learner_is_promoted_on_a_later_seat...`, `When_every_waitlisted_learner_is_ineligible...`; mutation M3 (skip-and-remove) is caught; guided demo steps 13-15 |
| A3 | **Only eligible learners may join the waitlist** (reject ineligible at enrollment) | Accepted (follows acceptance criterion 2) | `Enroll`, 422 mapping | `Ineligible_learner_does_not_join_the_waitlist_when_the_course_is_full`; mutation M9 caught |
| A4 | **Single `lock` over plain `List<string>` state** | Accepted | `EnrollmentService`, `docs/DESIGN.md` §7 | Five service concurrency tests plus an HTTP concurrency test; removing the lock (M1) is caught in 15 of 15 runs |
| A5 | **EF Core / SQLite persistence** (considered) | **Rejected** (brief: no new database) | none | n/a |
| A6 | **Configurable eligibility policy interface** (considered) | **Rejected** (brief: no configurable policy engine); eligibility is one private method | `IsEligible` | n/a |
| A7 | **Invariant I5: fill open seats from the queue before a new enrollee can take one** | Accepted; designed in from the first version | `Enroll`, `A_new_enrollee_cannot_jump_a_waitlisted_learner_who_has_become_eligible` | Mutation M4 (pre-fill removed) is caught by exactly that test |
| A8 | **HTTP mapping** 201 / 202 / 409 / 422 / 404 / 400 | Accepted for now (see section 5) | `Endpoints.cs` | API tests assert each code and body status |
| A9 | **Test shim** instead of xUnit (no NuGet access in the sandbox) | Accepted as a documented trade-off | `MiniXunit.cs`, README | Shim bug found and fixed, see section 3 |
| A10 | **Initial test run: 19 of 30 tests failed** | AI test-shim defect (not a product defect): `Assert.Equal` compared collections by reference | `MiniXunit.cs` | Fixed to compare element by element; re-ran: 30/30 passed. An example of AI output that looked finished but was wrong until run |
| A11 | **Mutation M8 (first version) survived** | Analysed, **edited**: the mutation was effectively equivalent, so it was replaced by real defects M8 (waitlist removal does nothing) and M10 (drop does not promote); the original is kept as M11 and accepted as benign | `docs/LIMITATIONS.md` item 13 | M8 and M10 are caught |
| A12 | **Lock-removal detection was weak** (only 1 test caught M1 at first) | **Edited**: added `Concurrent_promotion_attempts_into_one_open_seat...` with a deliberately slow records lookup to widen the check-then-act window | `ConcurrencyTests.cs`, `TestSupport.cs` | M1 caught in 15/15 runs (1-5 failing tests per run) |
| A13 | **Retarget from .NET 8 to .NET 10 LTS** (.NET 8 support ended on 10 November 2026), requested in P5. Also adopted `System.Threading.Lock`, collection expressions `[]`, and the `ThrowIf...` guard helpers | Accepted | all `.csproj` files, `global.json`, `EnrollmentService.cs`, `Endpoints.cs`, README | Clean build with warnings as errors; all tests; repeat runs; all real mutants caught again; live API smoke test |
| A14 | **My idea (P7):** reviewers should not have to type many commands; the app itself should ask which learners to enroll, possibly using AI. **AI response:** advised against adding runtime AI to the app (it would need an API key and credentials, and is outside the brief's scope) and proposed an interactive console with sample learners preloaded plus a guided demo | **Edited**: kept my goal, rejected runtime AI, accepted the console approach | New `src/CourseWaitlist.Cli` (`CliApp.cs`, `Program.cs`), `InMemoryLearnerRecords.All()`, README "Quick look" | Console tests driving the menu with scripted input (7 in the final version); `--demo` run: 16/16 checks matched; scripted interactive run inspected by hand |
| A15 | **README PowerShell error snippet** (`catch { $_.ErrorDetails.Message }`) | **Rejected after I tested it**: on my Windows PowerShell it printed nothing for the 422 response. Replaced with a `Show-Body` helper that falls back to reading the response stream | README "Try the HTTP API" (now in `docs/API_GUIDE.md`) | The stream-reading command showed `NotEligible ... has not completed the prerequisite 'CS101'` on my machine. The combined helper has not itself been run (`docs/LIMITATIONS.md` item 12) |
| A17 | **Investigation of "1 is enrolled even though 1 was not present" (P8).** Not a service defect: in that session I had first used option 4 (Add/update learner) and typed `1` as the learner ID with CS101, which created a learner whose ID was "1"; enrolling "1" was therefore correct, and "2" was correctly unknown. The real problem was usability: the console never showed which learners existed or what to type | Accepted. **Edited the console design**: learners shown in a numbered table (ID, name, completed courses, prerequisite met, status); a learner is chosen by row number, ID or part of the name; IDs are generated by the console (L101...), so a typed number always means a table row and can never become an ID; messages show names; a one-line seat/waitlist summary after every change | `CliApp.cs` (rewritten), README console section | `Typing_a_number_never_creates_or_enrolls_a_learner_with_that_id`, `Row_numbers_select_learners...`, `Ineligible_out_of_range_unknown_and_ambiguous_input...`, `Start_screen_shows_the_course_prerequisite_and_a_learner_table`; scripted interactive run inspected by hand |
| A18 | **Realistic data and a .NET course (P8):** sample learners renamed to Indian and American names (Ramesh Kumar, Priya Sharma, Arjun Reddy, Ananya Iyer, John Miller, Emily Davis; Rakesh Verma and Sarah Johnson without the prerequisite); offering renamed to `NET301` ASP.NET Core Web API Development with prerequisite `CSH101` C# Fundamentals (the same idea as JavaScript before React) | Accepted | Console, API defaults (`appsettings.json`, `OfferingOptions`), test data, README examples | Full suite 46/46; guided demo 16/16 with the new names; live API smoke test with the README's new IDs (201, 201, 202, 422, 200 with promotion, 404) |
| A16 | **Gap check against the colleague's instructions file (P3):** most items there (runtime model adapter, orchestration engine, three scenarios, audit metrics) do not apply to this brief; real gaps were my blank review section, unverified PowerShell snippets, no starter project, and docs written after code | Accepted | This record, README, `docs/LIMITATIONS.md` | Gaps closed in this record and by my own runs in section 5 |

## 3. Notable corrections found during review

1. **Shim defect (A10):** 19 false failures from reference comparison; fixed.
2. **Compile-blocked mutants:** M5, M6 and M9 first failed to build because the project treats warnings as errors (the mutation created unreachable code). Mutant builds now relax that setting; the product code still builds with warnings as errors.
3. **Weak concurrency evidence (A12):** strengthened as above.
4. **README snippet did not show error bodies on Windows PowerShell (A15):** found by me while testing by hand; fixed.

## 4. Verification evidence (re-run these yourself)

AI environment: .NET SDK 10.0.115 (runtime 10.0.15), Linux. My environment: Windows, .NET 10 SDK (section 5).

| Check | Result |
|---|---|
| `dotnet build CourseWaitlist.sln` (warnings are errors) | 0 warnings, 0 errors |
| `dotnet run --project tests/CourseWaitlist.Tests` | **46 passed, 0 failed**: 31 service tests (including 5 concurrency tests), 8 HTTP tests, 7 console tests |
| Repeat runs to detect flakiness | 10 / 10 fully green |
| `dotnet run --project src/CourseWaitlist.Cli -- --demo` | `Demo result: 16/16 checks matched the expected behaviour.` exit code 0 |
| Live smoke test of the running API on .NET 10 (curl) | 201, 201, 202, 422, 409, 404; drop promoted the first waitlisted learner; repeated drop returned 404 `NotEnrolled`; final state matched |
| Randomised test | 3000 random enroll/drop operations checked against a simple reference model after every step (fixed seed) |

**Mutation checks** (a defect is injected into a scratch copy; the suite must fail). Numbers are from the final 46-test suite:

| Id | Injected defect | Failing tests | Result |
|---|---|---|---|
| M1 | remove the lock | 1-5 per run; caught in 15/15 runs (re-checked on the final suite) | caught |
| M2 | LIFO instead of FIFO (join at the front) | 14 | caught |
| M3 | skip-and-**remove** instead of skip-and-retain | 8 | caught |
| M4 | remove "front of waitlist first" pre-fill (I5) | 1 | caught |
| M5 | no duplicate check for waitlisted learners | 5 | caught |
| M6 | promotion ignores eligibility | 7 | caught |
| M7 | capacity off-by-one (`<=`) | 21 | caught |
| M8 | waitlist removal reports success but does not remove | 2 | caught |
| M9 | no prerequisite check at enrollment | 6 | caught |
| M10 | drop does not promote anyone | 12 | caught |
| M11 | a drop of a non-member also fills open seats (benign) | 0 | survived: equivalent, accepted |

## 5. Candidate review and verification (mine)

Items marked **To complete** still need my input before submission.

**Understanding the brief and the starting point**
- I read the brief (text and PDF). No starter project and no rubric file (`Course-Registration-Rubric-Score.md`) were supplied to me, so I
  accepted the brief's fallback: a minimal service with an in-memory store.
- I compared the work with a stricter engineering-instructions file a colleague used for a different assignment (P3). Most of it
  (runtime model adapter, orchestration engine, three scenarios) does not apply to this bounded brief; the gaps it exposed were my own
  review, untested PowerShell steps and the missing starter, which this section addresses.

**What I reviewed**
- The solution structure: `src/CourseWaitlist.Api` (`AppHost`, `Endpoints`, `OfferingOptions`, `Program`), `src/CourseWaitlist.Core`
  (`EnrollmentService`, `InMemoryLearnerRecords`, `Models`) and `tests`. I checked that `Program.cs` only calls `AppHost.Build(args)` and
  `Run()`, and that all setup lives in `AppHost` so tests can host the API in-process.
- That "in memory" means data lives only while the process runs and is entered through the API (or the console).
- **To complete:** line-by-line notes on `EnrollmentService` (enroll, drop, `FillOpenSeats`) and anything I would change.

**What I verified myself, on Windows with the .NET 10 SDK**
1. Downloaded the zip, extracted it (running from inside the zip path does not work), and built the solution successfully.
2. Ran the API (`dotnet run --urls http://localhost:5090`) and exercised every main case by hand from Windows PowerShell:
   - Created learners `ana`, `bob`, `cy` (completed CS101) and `zed` (none): each returned `Updated`. I questioned why
     `promotedLearnerIds` was empty; it is expected, because nobody was waitlisted yet.
   - Enrolled `ana` and `bob`: `Enrolled`. Enrolled `cy`: `Waitlisted` at position 1 (capacity 2).
   - Enrolled `zed`: HTTP 422. The README's `catch { $_.ErrorDetails.Message }` printed nothing, and the raw error only showed
     "422 Unprocessable Entity". I expected the clear `NotEligible` reason, not just a status code. Reading the response stream showed
     `NotEligible: Learner 'zed' has not completed the prerequisite 'CS101'`. I reported this and the README was corrected (A15).
   - Dropped `ana`: `Dropped`, and `cy` was promoted from the waitlist (FIFO).
   - Dropped `ana` again: `NotEnrolled`, "nothing changed", with empty promoted and skipped lists.
   - `GET /offering`: CS201, capacity 2, 0 seats available, enrolled `bob` and `cy`, waitlist empty. All as expected.
     (These runs used the earlier sample names and course codes, before the change to NET301 / CSH101 in A18.)
3. Ran the automated tests on my machine before the console was added: **39 passed, 0 failed**.
4. Ran the guided demo (`dotnet run --project src\CourseWaitlist.Cli -- --demo`) and read its step-by-step output.
5. Tried the first interactive console as a reviewer would, and found it was not usable without help:
   - Nothing showed which learners existed or what to type as a "learner id".
   - I used option 4 with ID `1`, then enrolled `1` and got "Enrolled", while `2` was "UnknownLearner". That looked like a bug. On
     investigation it was correct behaviour (option 4 had created a learner with ID "1"), but it showed the design was confusing (A17).
   - The sample names (ana, bob, cy, dee, zed) did not look realistic, and the course had no meaningful name.
   I asked for real Indian and American names, a table showing who has the prerequisite and their status, and a .NET-related course
   with a prerequisite, the way JavaScript comes before React (A17, A18).
6. **To complete:** re-run on my machine after these changes: `dotnet run --project tests\CourseWaitlist.Tests` (expected 46 passed),
   the demo (expected 16/16), and the new console (`dotnet run --project src\CourseWaitlist.Cli`).

**What I changed or asked to change, and why**
- **Retarget to .NET 10 (P5, A13):** .NET 8 support ended on 10 November 2026; I did not want to submit on an unsupported runtime.
- **Reviewer experience (P7, A14):** while testing I noticed a reviewer would have to type many PowerShell commands and follow README
  steps just to see the behaviour. My first idea was for the app itself to ask which learners to enroll, possibly with AI. I accepted the
  advice not to add runtime AI (credentials and scope) and asked for an interactive console with sample data and a one-command guided
  demo instead.
- **README error handling (A15):** replaced the snippet that showed nothing on my PowerShell.
- **Console usability (P8, A17, A18):** numbered learner table, selection by row number / ID / name, auto-generated IDs, realistic
  names, and a .NET course (`NET301` ASP.NET Core Web API Development, prerequisite `CSH101` C# Fundamentals).

**What I rejected**
- Runtime AI inside the application (A14).
- The original PowerShell error snippet (A15).
- The first console design, where learner IDs were free text and invisible (A17).

**Decisions still to confirm (To complete)**
- Skip-and-retain policy for an ineligible learner at the front of the queue (A2).
- HTTP status codes: 409 for duplicates, 422 for a missing prerequisite, 404 for a repeated drop (A8).

**What I could not verify**
- Behaviour under real multi-user load beyond the automated concurrency tests.
- The combined `Show-Body` helper in `docs/API_GUIDE.md` as a whole, and anything on PowerShell 7.
- **To complete:** anything else I could not check.
