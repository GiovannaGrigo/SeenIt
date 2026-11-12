using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace SeenIt.Api.External;

// Dedicated bounded cache: never change limits of the application's existing shared cache.
public sealed class CatalogCache : IDisposable
{
    private sealed class Flight
    {
        public TaskCompletionSource<object> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Invalidated;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Flight> flights = [];
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 16 * 1024 * 1024 });
    private int consumers;

    public async Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> fetch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Flight flight;
        bool owner;
        lock (gate)
        {
            if (cache.TryGetValue<T>(key, out var value)) return value!;
            if (consumers >= 128) throw new Services.CatalogException("Catalog consumer capacity.");
            owner = !flights.TryGetValue(key, out flight!);
            if (owner)
            {
                if (flights.Count >= 32) throw new Services.CatalogException("Catalog flight capacity.");
                flights.Add(key, flight = new Flight());
            }
            consumers++;
        }
        if (owner) _ = ProduceAsync(key, flight, fetch);
        try { return (T)await flight.Completion.Task.WaitAsync(ct); }
        finally { lock (gate) consumers--; }
    }

    private async Task ProduceAsync<T>(string key, Flight flight, Func<CancellationToken, Task<T>> fetch)
    {
        try
        {
            // The first caller does not own the lifetime of a shared operation.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var value = await fetch(timeout.Token);
            var size = JsonSerializer.SerializeToUtf8Bytes(value).Length;
            lock (gate)
            {
                if (value is not null && !flight.Invalidated && size <= 2 * 1024 * 1024 && cache.Count < 512)
                    cache.Set(key, value, new MemoryCacheEntryOptions { Size = Math.Max(1, size), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
                flights.Remove(key);
                if (flight.Invalidated)
                {
                    flight.Completion.TrySetException(new Services.CatalogException("Catalog invalidated during fetch."));
                    _ = flight.Completion.Task.Exception;
                }
                else flight.Completion.TrySetResult(value!);
            }
        }
        catch (Exception error)
        {
            lock (gate) { flights.Remove(key); flight.Completion.TrySetException(error); }
            // Observe failures even when every consumer canceled.
            _ = flight.Completion.Task.Exception;
        }
    }

    public void Invalidate(string key)
    {
        lock (gate)
        {
            cache.Remove(key);
            if (flights.TryGetValue(key, out var flight)) flight.Invalidated = true;
        }
    }

    public void Dispose() => cache.Dispose();
}
