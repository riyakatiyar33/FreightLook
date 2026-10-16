using System.Data.Common;

namespace Data;

public interface IDbConnectionFactory
{
    /// <summary>Creates and opens a new connection. The caller disposes it.</summary>
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
