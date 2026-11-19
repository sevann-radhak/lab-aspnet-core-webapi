using LabAspNetCoreWebApi.Api;

namespace LabAspNetCoreWebApi.Api.Services;

public interface IInstanceProbe
{
    Guid InstanceId { get; }
}

public sealed class LifetimeRegistry
{
    private int _transient;
    private int _scoped;
    private int _singleton;

    public CreatedCounts CreatedCounts => new(_transient, _scoped, _singleton);

    public void MarkTransient()
    {
        _ = Interlocked.Increment(ref _transient);
    }

    public void MarkScoped()
    {
        _ = Interlocked.Increment(ref _scoped);
    }

    public void MarkSingleton()
    {
        _ = Interlocked.Increment(ref _singleton);
    }
}

public sealed class TransientProbe : IInstanceProbe
{
    public TransientProbe(LifetimeRegistry registry)
    {
        registry.MarkTransient();
        InstanceId = Guid.NewGuid();
    }

    public Guid InstanceId { get; }
}

public sealed class ScopedProbe : IInstanceProbe
{
    public ScopedProbe(LifetimeRegistry registry)
    {
        registry.MarkScoped();
        InstanceId = Guid.NewGuid();
    }

    public Guid InstanceId { get; }
}

public sealed class SingletonProbe : IInstanceProbe
{
    public SingletonProbe(LifetimeRegistry registry)
    {
        registry.MarkSingleton();
        InstanceId = Guid.NewGuid();
    }

    public Guid InstanceId { get; }
}
