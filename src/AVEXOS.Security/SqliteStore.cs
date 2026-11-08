using System.Text.Json;
using AVEXOS.Core;
using Microsoft.Data.Sqlite;

namespace AVEXOS.Security;

public sealed class SqliteStore : IAuditLog, IOperationStore, ISettingsStore
{
    private readonly string _dbPath;
    private readonly IClock _clock;
    private readonly object _lock = new();

    public SqliteStore(string dbPath, IClock clock)
    {
        _dbPath = dbPath;
        _clock = clock;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        using var conn = Open();
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS AuditEvents (
                Utc TEXT NOT NULL,
                Category TEXT NOT NULL,
                TargetId TEXT NOT NULL,
                OperationId TEXT,
                Outcome TEXT NOT NULL,
                Detail TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Operations (
                Id TEXT PRIMARY KEY,
                TargetId TEXT NOT NULL,
                Kind TEXT NOT NULL,
                Risk TEXT NOT NULL,
                State TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL,
                Detail TEXT);
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL);
            """);
    }

    public void Append(AuditEntry entry)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute(
                "INSERT INTO AuditEvents (Utc, Category, TargetId, OperationId, Outcome, Detail) VALUES (@u,@c,@t,@o,@o2,@d)",
                new { u = entry.Utc.ToString("o"), c = entry.Category, t = entry.TargetId, o = entry.OperationId, o2 = entry.Outcome, d = entry.Detail });
        }
    }

    IReadOnlyList<AuditEntry> IAuditLog.Recent(int count)
    {
        lock (_lock)
        {
            using var conn = Open();
            var rows = conn.Query("SELECT Utc, Category, TargetId, OperationId, Outcome, Detail FROM AuditEvents ORDER BY Utc DESC LIMIT @n", new { n = count });
            return rows.Select(r => new AuditEntry(
                DateTimeOffset.Parse(r["Utc"] as string ?? "1970-01-01T00:00:00Z"),
                r["Category"] as string ?? "",
                r["TargetId"] as string ?? "",
                r["OperationId"] as string,
                r["Outcome"] as string ?? "",
                r["Detail"] as string ?? "")).ToList();
        }
    }

    public void Save(OperationRecord record)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute(
                "INSERT OR REPLACE INTO Operations (Id, TargetId, Kind, Risk, State, UpdatedUtc, Detail) VALUES (@i,@t,@k,@r,@s,@u,@d)",
                new { i = record.Id, t = record.TargetId, k = record.Kind, r = record.Risk.ToString(), s = record.State.ToString(), u = record.UpdatedUtc.ToString("o"), d = record.Detail });
        }
    }

    IReadOnlyList<OperationRecord> IOperationStore.Recent(int count)
    {
        lock (_lock)
        {
            using var conn = Open();
            var rows = conn.Query("SELECT Id, TargetId, Kind, Risk, State, UpdatedUtc, Detail FROM Operations ORDER BY UpdatedUtc DESC LIMIT @n", new { n = count });
            return rows.Select(r => new OperationRecord(
                r["Id"] as string ?? "",
                r["TargetId"] as string ?? "",
                r["Kind"] as string ?? "",
                Enum.TryParse<RiskLevel>(r["Risk"] as string, true, out var rl) ? rl : RiskLevel.Low,
                Enum.TryParse<ChangeState>(r["State"] as string, true, out var cs) ? cs : ChangeState.Pending,
                DateTimeOffset.Parse(r["UpdatedUtc"] as string ?? "1970-01-01T00:00:00Z"),
                r["Detail"] as string ?? "")).ToList();
        }
    }

    public int CountByState(ChangeState state)
    {
        lock (_lock)
        {
            using var conn = Open();
            var row = conn.QueryOne("SELECT COUNT(*) AS n FROM Operations WHERE State = @s", new { s = state.ToString() });
            return Convert.ToInt32(row?["n"]);
        }
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            using var conn = Open();
            var row = conn.QueryOne("SELECT Value FROM Settings WHERE Key = @k", new { k = key });
            return row?["Value"] as string;
        }
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k,@v)", new { k = key, v = value });
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath};Cache=Shared");
        conn.Open();
        return conn;
    }
}

internal static class SqliteExtensions
{
    public static void Execute(this SqliteConnection conn, string sql, object? param = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (param is not null)
        {
            foreach (var p in param.GetType().GetProperties())
            {
                var pr = cmd.CreateParameter();
                pr.ParameterName = "@" + p.Name;
                pr.Value = p.GetValue(param) ?? DBNull.Value;
                cmd.Parameters.Add(pr);
            }
        }
        cmd.ExecuteNonQuery();
    }

    public static IReadOnlyList<IDictionary<string, object?>> Query(this SqliteConnection conn, string sql, object? param = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (param is not null)
        {
            foreach (var p in param.GetType().GetProperties())
            {
                var pr = cmd.CreateParameter();
                pr.ParameterName = "@" + p.Name;
                pr.Value = p.GetValue(param) ?? DBNull.Value;
                cmd.Parameters.Add(pr);
            }
        }
        var rows = new List<IDictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public static IDictionary<string, object?>? QueryOne(this SqliteConnection conn, string sql, object? param = null)
    {
        return conn.Query(sql, param).FirstOrDefault();
    }
}
