namespace Mkr.Core;

public sealed record Project(Guid Id, string Name, DateTimeOffset CreatedAt)
{
    public static Project Create(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is < 1 or > 120)
            throw new ArgumentException("Le nom doit contenir entre 1 et 120 caractères.", nameof(name));
        return new Project(Guid.NewGuid(), trimmed, DateTimeOffset.UtcNow);
    }
}

public interface IProjectStore
{
    IReadOnlyList<Project> List();
    void Add(Project project);
}

public interface IEngineDiagnostics
{
    Task<string> GetVersionAsync(CancellationToken cancellationToken);

    /// <summary>Refuses an engine whose WPF–Python contract is older than the one this application speaks (InvalidDataException with an explicit message).</summary>
    Task VerifyContractAsync(CancellationToken cancellationToken);
}
