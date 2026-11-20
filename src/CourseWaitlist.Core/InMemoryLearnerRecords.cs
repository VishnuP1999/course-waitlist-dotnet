using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace CourseWaitlist.Core;

/// <summary>Thread-safe in-memory learner records (stands in for a real student-records source).</summary>
public sealed class InMemoryLearnerRecords : ILearnerRecords
{
    private readonly ConcurrentDictionary<string, Learner> _learners = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a learner's completed-course list.</summary>
    public void Upsert(string learnerId, IEnumerable<string> completedCourses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learnerId);
        var id = learnerId.Trim();
        var courses = new HashSet<string>(
            completedCourses.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()),
            StringComparer.OrdinalIgnoreCase);
        _learners[id] = new Learner(id, courses);
    }

    public bool TryGet(string learnerId, [NotNullWhen(true)] out Learner? learner) =>
        _learners.TryGetValue(learnerId, out learner);

    /// <summary>All learners, ordered by id (used by the console to list them).</summary>
    public IReadOnlyList<Learner> All() =>
        _learners.Values.OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
}
