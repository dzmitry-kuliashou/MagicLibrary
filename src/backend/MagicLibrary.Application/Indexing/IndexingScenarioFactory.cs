using MagicLibrary.Core.Aggregates;

namespace MagicLibrary.Application.Indexing;

public sealed class IndexingScenarioFactory
{
    private readonly IReadOnlyList<IIndexingScenario> _scenarios;

    public IndexingScenarioFactory(IEnumerable<IIndexingScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        _scenarios = scenarios.ToArray();
    }

    public IIndexingScenario GetScenario(IndexingJob indexingJob)
    {
        ArgumentNullException.ThrowIfNull(indexingJob);

        var matchingScenarios = _scenarios
            .Where(scenario => scenario.CanProcess(indexingJob))
            .ToArray();
        return matchingScenarios.Length switch
        {
            1 => matchingScenarios[0],
            0 => throw new InvalidOperationException(
                $"No indexing scenario can process job '{indexingJob.Id}'."),
            _ => throw new InvalidOperationException(
                $"Multiple indexing scenarios can process job '{indexingJob.Id}'.")
        };
    }
}
