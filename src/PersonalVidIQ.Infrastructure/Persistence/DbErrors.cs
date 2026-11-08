using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PersonalVidIQ.Infrastructure.Persistence;

internal static class DbErrors
{
    /// <summary>True when the save failed on a unique constraint, e.g. another process inserted the same row first.</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
