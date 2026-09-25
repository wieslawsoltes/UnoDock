using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace UnoDock.Testing;

public sealed class TestRunner
{
    private readonly List<(string Name, Func<Task> Body)> _cases = [];
    public void Test(string name, Action body) => _cases.Add((name, () => { body(); return Task.CompletedTask; }));
    public void Test(string name, Func<Task> body) => _cases.Add((name, body));
    public async Task<int> Run(string directory, string suite)
    {
        var results = new List<TestResult>();
        foreach (var (name, body) in _cases)
        {
            var watch = Stopwatch.StartNew(); string? error = null;
            try { await body(); } catch (Exception ex) { error = ex.ToString(); }
            results.Add(new(name, watch.Elapsed.TotalSeconds, error));
            Console.WriteLine($"{(error == null ? "PASS" : "FAIL")} {name}{(error == null ? "" : "\n" + error)}");
        }
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, suite + ".json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        new XDocument(new XElement("testsuite", new XAttribute("name", suite), new XAttribute("tests", results.Count),
            new XAttribute("failures", results.Count(r => r.Error != null)), results.Select(r => new XElement("testcase", new XAttribute("name", r.Name),
                new XAttribute("time", r.Seconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                r.Error == null ? null : new XElement("failure", r.Error)))))
            .Save(Path.Combine(directory, suite + ".xml"));
        Console.WriteLine($"{suite}: {results.Count - results.Count(r => r.Error != null)}/{results.Count} passed");
        return results.Any(r => r.Error != null) ? 1 : 0;
    }
    public sealed record TestResult(string Name, double Seconds, string? Error);
}
