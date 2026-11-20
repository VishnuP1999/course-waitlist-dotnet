using System.Text.Json.Serialization;
using CourseWaitlist.Core;
using Microsoft.Extensions.Options;

namespace CourseWaitlist.Api;

/// <summary>Composition root. Kept separate from Program so tests can host the API in-process.</summary>
public static class AppHost
{
    public static WebApplication Build(string[] args, Action<OfferingOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<OfferingOptions>(builder.Configuration.GetSection("Offering"));
        if (configure is not null) builder.Services.PostConfigure(configure);

        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        // One in-memory store and ONE service instance: the service's lock is the transaction boundary.
        builder.Services.AddSingleton<InMemoryLearnerRecords>();
        builder.Services.AddSingleton<ILearnerRecords>(sp => sp.GetRequiredService<InMemoryLearnerRecords>());
        builder.Services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<OfferingOptions>>().Value;
            return new EnrollmentService(
                new OfferingConfig(o.OfferingId, o.Capacity, string.IsNullOrWhiteSpace(o.RequiredPrerequisite) ? null : o.RequiredPrerequisite.Trim()),
                sp.GetRequiredService<ILearnerRecords>());
        });

        var app = builder.Build();
        Endpoints.Map(app);
        return app;
    }
}
