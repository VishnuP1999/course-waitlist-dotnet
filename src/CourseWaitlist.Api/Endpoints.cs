using CourseWaitlist.Core;

namespace CourseWaitlist.Api;

public sealed record EnrollRequest(string? LearnerId);

public sealed record LearnerRequest(string[]? CompletedCourses);

/// <summary>Uniform response body so every outcome is explicit.</summary>
public sealed record ApiResponse(
    string Status,
    string Message,
    int? WaitlistPosition = null,
    IReadOnlyList<string>? PromotedLearnerIds = null,
    IReadOnlyList<string>? SkippedLearnerIds = null);

public static class Endpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        app.MapGet("/offering", (EnrollmentService service) => Results.Ok(service.Snapshot()));

        app.MapPost("/offering/enrollments", Enroll);
        app.MapDelete("/offering/enrollments/{learnerId}", Drop);

        // Stand-in for a real student-records source (identity integrations are out of scope).
        app.MapPut("/learners/{learnerId}", UpsertLearner);
    }

    private static IResult Enroll(EnrollRequest? request, EnrollmentService service)
    {
        var result = service.Enroll(request?.LearnerId);
        var body = new ApiResponse(result.Status.ToString(), result.Message, result.WaitlistPosition);

        return result.Status switch
        {
            EnrollStatus.Enrolled => Results.Json(body, statusCode: StatusCodes.Status201Created),
            EnrollStatus.Waitlisted => Results.Json(body, statusCode: StatusCodes.Status202Accepted),
            EnrollStatus.AlreadyEnrolled or EnrollStatus.AlreadyWaitlisted => Results.Json(body, statusCode: StatusCodes.Status409Conflict),
            EnrollStatus.NotEligible => Results.Json(body, statusCode: StatusCodes.Status422UnprocessableEntity),
            EnrollStatus.UnknownLearner => Results.Json(body, statusCode: StatusCodes.Status404NotFound),
            _ => Results.Json(body, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    private static IResult Drop(string learnerId, EnrollmentService service)
    {
        var result = service.Drop(learnerId);
        var body = new ApiResponse(result.Status.ToString(), result.Message, null, result.PromotedLearnerIds, result.SkippedLearnerIds);

        return result.Status switch
        {
            DropStatus.Dropped or DropStatus.RemovedFromWaitlist => Results.Json(body, statusCode: StatusCodes.Status200OK),
            DropStatus.NotEnrolled => Results.Json(body, statusCode: StatusCodes.Status404NotFound),
            _ => Results.Json(body, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    private static IResult UpsertLearner(string learnerId, LearnerRequest? request, InMemoryLearnerRecords records, EnrollmentService service)
    {
        if (string.IsNullOrWhiteSpace(learnerId) || learnerId.Length > 64)
            return Results.Json(new ApiResponse("InvalidRequest", "learnerId must be 1-64 characters."), statusCode: StatusCodes.Status400BadRequest);

        records.Upsert(learnerId, request?.CompletedCourses ?? []);

        // Records changed, so an earlier-skipped waitlisted learner may now qualify for an open seat.
        var promoted = service.FillOpenSeats();
        return Results.Json(new ApiResponse("Updated", $"Learner '{learnerId.Trim()}' records updated.", null, promoted));
    }
}
