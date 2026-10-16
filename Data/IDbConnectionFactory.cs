using System.Data.Common;

namespace Data;

public interface IDbConnectionFactory
{
    /// <summary>Creates a new (closed) connection to the FreightLook database. The caller disposes it.</summary>
    DbConnection CreateConnection();
}
