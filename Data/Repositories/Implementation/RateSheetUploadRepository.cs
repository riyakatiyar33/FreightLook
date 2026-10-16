using System.Data;
using Dapper;
using Data.Repositories.Interfaces;
using Models.Entities;

namespace Data.Repositories.Implementation;

public sealed class RateSheetUploadRepository : IRateSheetUploadRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RateSheetUploadRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InsertAsync(RateSheetUpload upload, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "dbo.usp_RateSheetUpload_Insert",
            new
            {
                upload.FileName,
                upload.ParsedCount,
                upload.SavedCount,
                upload.RejectedCount
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }
}
