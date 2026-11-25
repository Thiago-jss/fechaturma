using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FechaTurma.Core;
#if NETFRAMEWORK
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif

namespace FechaTurma.Data
{
    public sealed class SqlClosureStore : IClosureStore
    {
        private const string Columns = "Id, Organization, Term, ClassCode, Title, Version, StudentCount, ApprovedCount, ImportedAt, ClosedAt";
        private readonly string connectionString;
        public SqlClosureStore(string connectionString) { this.connectionString = connectionString; }

        public ImportResult Import(Preview snapshot, string actor)
        {
            if (snapshot.Fingerprint == null) throw new WorkflowException("invalid_snapshot");
            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                using (var query = Create(connection, tx, @"SELECT Id, Fingerprint FROM dbo.Closures WITH (UPDLOCK, HOLDLOCK)
WHERE Organization=@org AND Term=@term AND ClassCode=@class"))
                {
                    Key(query, snapshot.Key);
                    using (var reader = query.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (reader.GetString(1) != snapshot.Fingerprint) throw new WorkflowException("snapshot_already_exists");
                            var current = reader.GetGuid(0);
                            reader.Close();
                            tx.Commit();
                            return new ImportResult(current, false);
                        }
                    }
                }
                var id = Guid.NewGuid();
                var at = DateTimeOffset.UtcNow;
                using (var insert = Create(connection, tx, @"INSERT INTO dbo.Closures
(Id,Organization,Term,ClassCode,Title,Fingerprint,Version,StudentCount,ApprovedCount,ImportedAt)
VALUES (@id,@org,@term,@class,@title,@fingerprint,1,@students,@approved,@at)"))
                {
                    Key(insert, snapshot.Key);
                    Param(insert, "@id", SqlDbType.UniqueIdentifier, id);
                    Param(insert, "@title", SqlDbType.NVarChar, snapshot.Title, 120);
                    Param(insert, "@fingerprint", SqlDbType.Char, snapshot.Fingerprint, 64);
                    Param(insert, "@students", SqlDbType.Int, snapshot.Rows.Count);
                    Param(insert, "@approved", SqlDbType.Int, snapshot.Rows.Count(r => r.Outcome == "approved"));
                    Param(insert, "@at", SqlDbType.DateTimeOffset, at);
                    insert.ExecuteNonQuery();
                }
                AddRows(connection, tx, id, snapshot.Rows);
                AddAudit(connection, tx, id, 1, actor, at);
                tx.Commit();
                return new ImportResult(id, true);
            }
        }

        public IReadOnlyList<ClosureSummary> List(int organization)
        {
            using (var connection = Open())
            using (var command = Create(connection, null, "SELECT TOP(100) " + Columns +
                " FROM dbo.Closures WHERE Organization=@org ORDER BY ImportedAt DESC, Id DESC"))
            {
                Param(command, "@org", SqlDbType.Int, organization);
                var items = new List<ClosureSummary>();
                using (var reader = command.ExecuteReader()) while (reader.Read()) items.Add(Summary(reader));
                return items.AsReadOnly();
            }
        }

        public Closure Find(int organization, Guid id)
        {
            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.RepeatableRead))
            {
                var result = Find(connection, tx, id, organization);
                tx.Commit();
                return result;
            }
        }

        public Closure Close(int organization, Guid id, int expectedVersion, string actor)
        {
            using (var connection = Open())
            using (var tx = connection.BeginTransaction())
            {
                var at = DateTimeOffset.UtcNow;
                using (var update = Create(connection, tx, @"UPDATE dbo.Closures SET Version=2, ClosedAt=@at
WHERE Organization=@org AND Id=@id AND Version=@version AND ClosedAt IS NULL"))
                {
                    Scope(update, id, organization);
                    Param(update, "@version", SqlDbType.Int, expectedVersion);
                    Param(update, "@at", SqlDbType.DateTimeOffset, at);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        if (Find(connection, tx, id, organization) == null) throw new WorkflowException("closure_not_found");
                        throw new WorkflowException("closure_conflict");
                    }
                }
                AddAudit(connection, tx, id, 2, actor, at);
                var result = Find(connection, tx, id, organization);
                tx.Commit();
                return result;
            }
        }

        private static Closure Find(SqlConnection connection, SqlTransaction tx, Guid id, int organization)
        {
            using (var query = Create(connection, tx, "SELECT " + Columns + @" FROM dbo.Closures WHERE Id=@id AND Organization=@org;
SELECT r.Registration, r.StudentName, r.Grade, r.Attendance, r.Approved FROM dbo.ClosureRows r INNER JOIN dbo.Closures c ON c.Id=r.ClosureId
WHERE c.Id=@id AND c.Organization=@org ORDER BY r.Registration;
SELECT a.Sequence, a.Actor, a.OccurredAt FROM dbo.ClosureAudit a INNER JOIN dbo.Closures c ON c.Id=a.ClosureId
WHERE c.Id=@id AND c.Organization=@org ORDER BY a.Sequence;"))
            {
                Scope(query, id, organization);
                using (var reader = query.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    var head = Summary(reader);
                    var rows = new List<ClosedRow>();
                    var audit = new List<AuditEntry>();
                    reader.NextResult();
                    while (reader.Read()) rows.Add(new ClosedRow(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetBoolean(4) ? "approved" : "failed"));
                    reader.NextResult();
                    while (reader.Read()) audit.Add(new AuditEntry(reader.GetByte(0) == 1 ? "imported" : "closed", reader.GetString(1), reader.GetDateTimeOffset(2)));
                    return new Closure(head, rows.AsReadOnly(), audit.AsReadOnly());
                }
            }
        }

        private static ClosureSummary Summary(SqlDataReader reader) => new ClosureSummary(reader.GetGuid(0),
            new ClassKey(reader.GetInt32(1), reader.GetString(2), reader.GetString(3)), reader.GetString(4), reader.GetInt32(5),
            reader.GetInt32(6), reader.GetInt32(7), reader.GetDateTimeOffset(8), reader.IsDBNull(9) ? (DateTimeOffset?)null : reader.GetDateTimeOffset(9));

        private static void AddRows(SqlConnection connection, SqlTransaction tx, Guid id, IEnumerable<PreviewRow> rows)
        {
            using (var command = Create(connection, tx, @"INSERT INTO dbo.ClosureRows (ClosureId,Registration,StudentName,Grade,Attendance,Approved)
VALUES (@id,@registration,@name,@grade,@attendance,@approved)"))
            {
                Param(command, "@id", SqlDbType.UniqueIdentifier, id);
                var registration = command.Parameters.Add("@registration", SqlDbType.VarChar, 24);
                var name = command.Parameters.Add("@name", SqlDbType.NVarChar, 100);
                var grade = command.Parameters.Add("@grade", SqlDbType.Decimal); grade.Precision = 4; grade.Scale = 2;
                var attendance = command.Parameters.Add("@attendance", SqlDbType.Decimal); attendance.Precision = 5; attendance.Scale = 2;
                var approved = command.Parameters.Add("@approved", SqlDbType.Bit);
                foreach (var row in rows)
                {
                    registration.Value = row.Registration; name.Value = row.Name;
                    grade.Value = row.Grade.Value; attendance.Value = row.Attendance.Value;
                    approved.Value = row.Outcome == "approved";
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void AddAudit(SqlConnection connection, SqlTransaction tx, Guid id, byte sequence, string actor, DateTimeOffset at)
        {
            using (var command = Create(connection, tx, "INSERT dbo.ClosureAudit (ClosureId,Sequence,Actor,OccurredAt) VALUES (@id,@seq,@actor,@at)"))
            {
                Param(command, "@id", SqlDbType.UniqueIdentifier, id);
                Param(command, "@seq", SqlDbType.TinyInt, sequence);
                Param(command, "@actor", SqlDbType.VarChar, actor, 40);
                Param(command, "@at", SqlDbType.DateTimeOffset, at);
                command.ExecuteNonQuery();
            }
        }

        private SqlConnection Open()
        {
            var connection = new SqlConnection(connectionString);
            try { connection.Open(); return connection; }
            catch { connection.Dispose(); throw; }
        }

        private static SqlCommand Create(SqlConnection connection, SqlTransaction tx, string sql) =>
            new SqlCommand(sql, connection, tx) { CommandTimeout = 8 };

        private static void Param(SqlCommand command, string name, SqlDbType type, object value, int size = 0) =>
            (size > 0 ? command.Parameters.Add(name, type, size) : command.Parameters.Add(name, type)).Value = value;

        private static void Scope(SqlCommand command, Guid id, int organization)
        {
            Param(command, "@id", SqlDbType.UniqueIdentifier, id);
            Param(command, "@org", SqlDbType.Int, organization);
        }

        private static void Key(SqlCommand command, ClassKey key)
        {
            Param(command, "@org", SqlDbType.Int, key.Organization);
            Param(command, "@term", SqlDbType.VarChar, key.Term, 6);
            Param(command, "@class", SqlDbType.VarChar, key.ClassCode, 24);
        }
    }
}
