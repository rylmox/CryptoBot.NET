namespace Engine.Strategies;

public interface IStrategy
{
    Task<bool> Initialize(CancellationToken ct);
    Task Execute(CancellationToken ct);
    Task Terminate(CancellationToken ct);
    string GetState();
}