# Course Waitlist

A C# / .NET 10 application that manages enrollment for **one course offering**: learners enroll, a FIFO waitlist forms when the course is full, and when someone drops, the earliest eligible waitlisted learner is promoted automatically.

## What this application does

- **Course:** `NET301` - ASP.NET Core Web API Development, capacity 2 seats.
- **Prerequisite:** `CSH101` - C# Fundamentals. Only learners who completed it can enroll or join the waitlist.
- **Enroll:** if a seat is free, the learner is enrolled. If the course is full, the learner joins the back of the waitlist.
- **Drop:** the seat opens and the earliest eligible learner on the waitlist is promoted (first in, first out).
- **Duplicates:** enrolling twice, or dropping twice, gives a clear message and changes nothing.
- **Ineligible learner at the front of the queue:** skipped, but keeps their place, and is checked again next time a seat opens.
- **Data:** kept in memory only. Everything resets when the program closes.

The project has four parts:

| Folder | What it is |
| --- | --- |
| `src/CourseWaitlist.Core` | The rules and state (enroll, drop, promotion, locking) |
| `src/CourseWaitlist.Cli` | Interactive console and guided demo |
| `src/CourseWaitlist.Api` | Optional HTTP API over the same rules |
| `tests/CourseWaitlist.Tests` | 46 automated tests |

## Prerequisites

- **.NET 10 SDK** (10.0.100 or later). Download: https://dotnet.microsoft.com/download/dotnet/10.0
- **Git**, to clone the repository. Download: https://git-scm.com/downloads
- Check it is installed:

```
dotnet --list-sdks
```

A version starting with `10.0` must appear. No NuGet packages are needed, so it builds offline.

## Step 1: Get the code and build

```
git clone https://github.com/VishnuP1999/course-waitlist-dotnet.git
cd course-waitlist-dotnet
dotnet build CourseWaitlist.sln
```

No Git? On the repository page choose Code → Download ZIP, extract it, and open a terminal in the extracted folder (course-waitlist-dotnet-main). Run all commands below from the folder that contains `CourseWaitlist.sln`.

## Step 2: Try it yourself (interactive console)

```
dotnet run --project src/CourseWaitlist.Cli
```

**What you will see:** the course name, the prerequisite, free seats, and a table of 8 sample learners showing their ID, name, completed courses, whether they have CSH101, and their current status.

| # | ID | Name | Has CSH101? |
| --- | --- | --- | --- |
| 1 | L101 | Ramesh Kumar | Yes |
| 2 | L102 | Priya Sharma | Yes |
| 3 | L103 | Arjun Reddy | Yes |
| 4 | L104 | Ananya Iyer | Yes |
| 5 | L105 | John Miller | Yes |
| 6 | L106 | Emily Davis | Yes |
| 7 | L107 | Rakesh Verma | No |
| 8 | L108 | Sarah Johnson | No |

**Menu:**

| Option | Action |
| --- | --- |
| 1 | Enroll a learner |
| 2 | Drop a learner |
| 3 | Show course and learners |
| 4 | Add a new learner (ID is assigned automatically) |
| 5 | Update a learner's completed courses |
| 6 | Run the guided demo |
| 7 | Reset to the starting state |
| 0 | Exit |

When asked for a learner, type the **row number** (`1`), the **ID** (`L101`) or part of the **name** (`Ramesh`).

**Suggested walkthrough:**

1. Choose `1`, then `1`: Ramesh Kumar is **Enrolled**.
2. Choose `1`, then `2`: Priya Sharma is **Enrolled** (course now full).
3. Choose `1`, then `3`: Arjun Reddy is **Waitlisted #1**.
4. Choose `1`, then `7`: Rakesh Verma is **NotEligible** (no CSH101).
5. Choose `1`, then `1` again: **AlreadyEnrolled**, nothing changes.
6. Choose `2`, then `1`: Ramesh drops and **Arjun is promoted** from the waitlist.
7. Choose `2`, then `1` again: **NotEnrolled**, nothing changes.
8. Choose `5`, pick `7`, enter `CSH101`: Rakesh becomes eligible and can now enroll.
9. Choose `0` to exit.

Optional settings: `--capacity <n>` and `--prerequisite <code|none>`, for example:

```
dotnet run --project src/CourseWaitlist.Cli -- --capacity 3
```

## Step 3: Run the guided demo

```
dotnet run --project src/CourseWaitlist.Cli -- --demo
```

The demo runs a full scenario by itself and prints the **expected and actual result for every step**. It checks:

- Enrolling while seats are free
- Joining the waitlist when the course is full, with the correct position
- Rejecting a learner without the prerequisite
- Duplicate enroll, for both an enrolled and a waitlisted learner
- A blank (invalid) learner ID
- FIFO promotion when an enrolled learner drops
- A repeated drop that changes nothing
- Skip-and-retain: a waitlisted learner who loses the prerequisite is skipped but keeps their place, and gets the next seat once eligible again

It ends with:

```
Demo result: 16/16 checks matched the expected behaviour.
```

Exit code `0` means every check matched.

## Step 4: Run all automated tests

```
dotnet run --project tests/CourseWaitlist.Tests
```

Expected result:

```
Total: 46  Passed: 46  Failed: 0
```

| Area | What is tested |
| --- | --- |
| Enrollment | Free seats, full course, duplicates, invalid IDs, prerequisite failure |
| Drop and promotion | FIFO order, repeated drops, skip-and-retain policy, random sequences against a reference model |
| Concurrency | Many learners enrolling and dropping at once never exceed capacity or create duplicates |
| HTTP API | Status codes and responses for every case |
| Console | Menu flow, row-number selection, clear error messages |

To run only some tests, add part of a test name:

```
dotnet run --project tests/CourseWaitlist.Tests -- Concurrency
```

The tests use a built-in xUnit-style runner, so the command is `dotnet run`, not `dotnet test`.

## Optional: HTTP API

```
dotnet run --project src/CourseWaitlist.Api --urls http://localhost:5090
```

| Request | Result |
| --- | --- |
| `PUT /learners/{id}` with `{"completedCourses":["CSH101"]}` | Create or update a learner |
| `POST /offering/enrollments` with `{"learnerId":"L101"}` | 201 Enrolled, 202 Waitlisted, 409 Duplicate, 422 Not eligible |
| `DELETE /offering/enrollments/{id}` | 200 Dropped (with promoted learner), 404 Not enrolled |
| `GET /offering` | Current seats, enrolled list and waitlist |

The API starts empty, so register learners first.

## More documentation

- `docs/API_GUIDE.md` - full HTTP API guide with PowerShell and bash examples, the request flow diagram, and test-runner notes
- `docs/DESIGN.md` - design, invariants, concurrency approach, trade-offs
- `docs/AI_USE_RECORD.md` - how AI was used and how its output was verified
- `docs/LIMITATIONS.md` - known limitations and follow-up risks
