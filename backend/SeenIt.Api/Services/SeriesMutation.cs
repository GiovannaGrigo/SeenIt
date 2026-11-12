using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SeenIt.Api.Data;

namespace SeenIt.Api.Services;

public sealed class MutationConflictException : Exception;

public static class SeriesMutation
{
    // All B03 writers acquire exactly one stable aggregate lock before reading DB state.
    // PostgreSQL owns the lock, including for aggregates with no row yet. No process-local gate.
    public static async Task<IDbContextTransaction> BeginAsync(AppDbContext db, Guid userId, int seriesId, CancellationToken ct)
    {
        var postgres = db.Database.IsNpgsql();
        var transaction = await db.Database.BeginTransactionAsync(
            postgres ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);
        try
        {
            if (postgres)
            {
                var digest = SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"seenit:b03:{userId:D}:{seriesId}")));
                var key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(digest);
                var acquired = await db.Database.SqlQueryRaw<bool>(
                    "SELECT pg_try_advisory_xact_lock({0}) AS \"Value\"", key).SingleAsync(ct);
                if (!acquired) throw new MutationConflictException();
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '1s'", ct);
                await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '5s'", ct);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
