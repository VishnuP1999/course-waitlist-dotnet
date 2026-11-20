// A tiny, dependency-free stand-in for xUnit, used because the build sandbox has no NuGet access.
// Tests are written exactly like xUnit tests ([Fact], Assert.*). To switch to real xUnit: delete this file and
// Program.cs, add PackageReference Microsoft.NET.Test.Sdk, xunit and xunit.runner.visualstudio (and remove the
// nuget.config that clears package sources), set OutputType to Library, then run `dotnet test`.

using System.Collections;
using System.Reflection;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FactAttribute : Attribute { }

    public sealed class AssertException : Exception
    {
        public AssertException(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void Equal<T>(T expected, T actual)
        {
            // Collections (other than string) are compared element by element, like xUnit does.
            if (expected is IEnumerable e && actual is IEnumerable a && expected is not string)
            {
                var el = e.Cast<object?>().ToList();
                var al = a.Cast<object?>().ToList();
                if (!el.SequenceEqual(al))
                    throw new AssertException($"Assert.Equal (sequence) failed.\n  Expected: [{string.Join(", ", el)}]\n  Actual:   [{string.Join(", ", al)}]");
                return;
            }

            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new AssertException($"Assert.Equal failed.\n  Expected: {expected}\n  Actual:   {actual}");
        }

        public static void NotEqual<T>(T notExpected, T actual)
        {
            if (EqualityComparer<T>.Default.Equals(notExpected, actual))
                throw new AssertException($"Assert.NotEqual failed. Value: {actual}");
        }

        public static void True(bool condition, string? message = null)
        {
            if (!condition) throw new AssertException(message ?? "Assert.True failed.");
        }

        public static void False(bool condition, string? message = null)
        {
            if (condition) throw new AssertException(message ?? "Assert.False failed.");
        }

        public static void Null(object? value)
        {
            if (value is not null) throw new AssertException($"Assert.Null failed. Value: {value}");
        }

        public static void NotNull(object? value)
        {
            if (value is null) throw new AssertException("Assert.NotNull failed.");
        }

        public static void Contains(string expectedSubstring, string? actual)
        {
            if (actual is null || !actual.Contains(expectedSubstring, StringComparison.Ordinal))
                throw new AssertException($"Assert.Contains failed.\n  Looking for: {expectedSubstring}\n  In:          {actual}");
        }

        public static void Contains<T>(T expected, IEnumerable<T> collection)
        {
            if (!collection.Contains(expected))
                throw new AssertException($"Assert.Contains failed. Item not found: {expected}");
        }

        public static void DoesNotContain<T>(T unexpected, IEnumerable<T> collection)
        {
            if (collection.Contains(unexpected))
                throw new AssertException($"Assert.DoesNotContain failed. Item found: {unexpected}");
        }

        public static void Empty(IEnumerable collection)
        {
            if (collection.Cast<object>().Any()) throw new AssertException("Assert.Empty failed.");
        }

        public static T Single<T>(IEnumerable<T> collection)
        {
            var items = collection.ToList();
            if (items.Count != 1) throw new AssertException($"Assert.Single failed. Expected 1 item but found {items.Count}.");
            return items[0];
        }

        public static T Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T ex) { return ex; }
            catch (Exception ex) { throw new AssertException($"Expected {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}"); }
            throw new AssertException($"Expected {typeof(T).Name} but nothing was thrown.");
        }
    }

    /// <summary>Discovers [Fact] methods by reflection and runs each on a fresh instance of its class.</summary>
    public static class MiniRunner
    {
        public static int Run(Assembly assembly, string[] args)
        {
            var filter = args.FirstOrDefault();
            var passed = 0;
            var failed = new List<string>();
            var started = DateTime.UtcNow;

            var tests = assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<FactAttribute>() is not null)
                    .Select(m => (Type: t, Method: m)))
                .Where(x => filter is null || $"{x.Type.Name}.{x.Method.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type.Name).ThenBy(x => x.Method.Name)
                .ToList();

            foreach (var (type, method) in tests)
            {
                var name = $"{type.Name}.{method.Name}";
                try
                {
                    var instance = Activator.CreateInstance(type);
                    var result = method.Invoke(instance, null);
                    if (result is Task task) task.GetAwaiter().GetResult();
                    passed++;
                    Console.WriteLine($"  PASS  {name}");
                }
                catch (Exception ex)
                {
                    var inner = ex is TargetInvocationException { InnerException: { } i } ? i : ex;
                    failed.Add(name);
                    Console.WriteLine($"  FAIL  {name}\n        {inner.GetType().Name}: {inner.Message.Replace("\n", "\n        ")}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {tests.Count}  Passed: {passed}  Failed: {failed.Count}  ({(DateTime.UtcNow - started).TotalSeconds:0.0}s)");
            return failed.Count == 0 ? 0 : 1;
        }
    }
}
