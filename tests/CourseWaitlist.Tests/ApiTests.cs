using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CourseWaitlist.Api;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace CourseWaitlist.Tests;

/// <summary>Runs the real API in-process on a random local port.</summary>
public sealed class ApiHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    public HttpClient Client { get; }

    private ApiHost(WebApplication app)
    {
        _app = app;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
    }

    public static async Task<ApiHost> StartAsync(int capacity = 2, string? prerequisite = "CSH101")
    {
        var app = AppHost.Build(
            new[] { "--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default=Warning", "--Logging:LogLevel:Microsoft=Warning" },
            o => { o.Capacity = capacity; o.RequiredPrerequisite = prerequisite; });
        await app.StartAsync();
        return new ApiHost(app);
    }

    public Task<HttpResponseMessage> PutLearner(string id, params string[] completed) =>
        Client.PutAsJsonAsync($"/learners/{id}", new { completedCourses = completed });

    public Task<HttpResponseMessage> Enroll(string? id) =>
        Client.PostAsJsonAsync("/offering/enrollments", new { learnerId = id });

    public Task<HttpResponseMessage> Drop(string id) => Client.DeleteAsync($"/offering/enrollments/{id}");

    public async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

public class ApiTests
{
    [Fact]
    public async Task Enroll_waitlist_and_drop_promotion_flow_over_http()
    {
        await using var host = await ApiHost.StartAsync(capacity: 1);
        foreach (var id in new[] { "ana", "bob", "cy" }) await host.PutLearner(id, "CSH101");

        var first = await host.Enroll("ana");
        var second = await host.Enroll("bob");
        var third = await host.Enroll("cy");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(1, (await host.Json(second)).GetProperty("waitlistPosition").GetInt32());
        Assert.Equal(2, (await host.Json(third)).GetProperty("waitlistPosition").GetInt32());

        var drop = await host.Drop("ana");
        Assert.Equal(HttpStatusCode.OK, drop.StatusCode);
        Assert.Equal("bob", (await host.Json(drop)).GetProperty("promotedLearnerIds")[0].GetString());

        var state = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal("bob", state.GetProperty("enrolled")[0].GetString());
        Assert.Equal("cy", state.GetProperty("waitlist")[0].GetString());
        Assert.Equal(0, state.GetProperty("seatsAvailable").GetInt32());
    }

    [Fact]
    public async Task Duplicate_enroll_returns_409_and_creates_no_second_record()
    {
        await using var host = await ApiHost.StartAsync(capacity: 1);
        await host.PutLearner("ana", "CSH101");
        await host.PutLearner("bob", "CSH101");
        await host.Enroll("ana");
        await host.Enroll("bob");

        var dupEnrolled = await host.Enroll("ana");
        var dupWaitlisted = await host.Enroll("bob");

        Assert.Equal(HttpStatusCode.Conflict, dupEnrolled.StatusCode);
        Assert.Equal("AlreadyEnrolled", (await host.Json(dupEnrolled)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, dupWaitlisted.StatusCode);
        Assert.Equal("AlreadyWaitlisted", (await host.Json(dupWaitlisted)).GetProperty("status").GetString());

        var state = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal(1, state.GetProperty("enrolled").GetArrayLength());
        Assert.Equal(1, state.GetProperty("waitlist").GetArrayLength());
    }

    [Fact]
    public async Task Prerequisite_failure_returns_422_and_unknown_learner_404()
    {
        await using var host = await ApiHost.StartAsync();
        await host.PutLearner("zed");   // no completed courses

        var ineligible = await host.Enroll("zed");
        var unknown = await host.Enroll("ghost");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ineligible.StatusCode);
        Assert.Equal("NotEligible", (await host.Json(ineligible)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var state = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal(0, state.GetProperty("enrolled").GetArrayLength());
        Assert.Equal(0, state.GetProperty("waitlist").GetArrayLength());
    }

    [Fact]
    public async Task Invalid_requests_return_400_with_a_clear_body()
    {
        await using var host = await ApiHost.StartAsync();

        var missing = await host.Enroll(null);
        var blank = await host.Enroll("   ");
        var emptyBody = await host.Client.PostAsync("/offering/enrollments", new StringContent("null", Encoding.UTF8, "application/json"));

        foreach (var response in new[] { missing, blank, emptyBody })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("InvalidRequest", (await host.Json(response)).GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Repeated_drop_returns_404_and_state_is_unchanged()
    {
        await using var host = await ApiHost.StartAsync(capacity: 1);
        await host.PutLearner("ana", "CSH101");
        await host.PutLearner("bob", "CSH101");
        await host.Enroll("ana");
        await host.Enroll("bob");

        var first = await host.Drop("ana");
        var second = await host.Drop("ana");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal("NotEnrolled", (await host.Json(second)).GetProperty("status").GetString());

        var state = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal("bob", state.GetProperty("enrolled")[0].GetString());
        Assert.Equal(0, state.GetProperty("waitlist").GetArrayLength());
    }

    [Fact]
    public async Task Recording_a_completed_prerequisite_promotes_a_skipped_waitlisted_learner()
    {
        await using var host = await ApiHost.StartAsync(capacity: 1);
        await host.PutLearner("ana", "CSH101");
        await host.PutLearner("bob", "CSH101");
        await host.Enroll("ana");
        await host.Enroll("bob");                // waitlisted
        await host.PutLearner("bob");            // bob's prerequisite record is removed
        await host.Drop("ana");                  // bob skipped, retained, seat stays open

        var stateBefore = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal(1, stateBefore.GetProperty("waitlist").GetArrayLength());
        Assert.Equal(1, stateBefore.GetProperty("seatsAvailable").GetInt32());

        var update = await host.PutLearner("bob", "CSH101");   // records fixed: promotion happens now

        Assert.Equal("bob", (await host.Json(update)).GetProperty("promotedLearnerIds")[0].GetString());
        var stateAfter = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal("bob", stateAfter.GetProperty("enrolled")[0].GetString());
    }

    [Fact]
    public async Task Concurrent_http_enrollments_never_oversell_the_course()
    {
        await using var host = await ApiHost.StartAsync(capacity: 3);
        var ids = Enumerable.Range(1, 60).Select(i => $"L{i}").ToList();
        foreach (var id in ids) await host.PutLearner(id, "CSH101");

        var responses = await Task.WhenAll(ids.Select(id => host.Enroll(id)));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(57, responses.Count(r => r.StatusCode == HttpStatusCode.Accepted));
        var state = await host.Json(await host.Client.GetAsync("/offering"));
        Assert.Equal(3, state.GetProperty("enrolled").GetArrayLength());
        Assert.Equal(57, state.GetProperty("waitlist").GetArrayLength());
    }

    [Fact]
    public async Task Health_endpoint_is_up()
    {
        await using var host = await ApiHost.StartAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/health")).StatusCode);
    }
}
