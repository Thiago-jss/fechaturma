using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FechaTurma.Core;
using FechaTurma.Data;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oracle.ManagedDataAccess.Client;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace FechaTurma.Tests
{
    [TestClass]
    public sealed class DatabaseTests
    {
        private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Configure " + name);
        private static string Sql => Required("FT_SQL_CONNECTION");
        private static string Oracle => Required("FT_ORACLE_CONNECTION");
        private static ClassKey Demo(int organization, string code = "PI-01") => new ClassKey(organization, "2026.2", code);

        [TestMethod]
        public void OracleReadsScopedSnapshotAndPreservesMissingGradeWithoutWritePermission()
        {
            var source = new OracleAcademicSource(Oracle);
            var complete = ClosingPolicy.Check(Demo(1), source.Read(Demo(1)));
            var expected = ClosingPolicy.Check(Demo(1), new SyntheticAcademicSource().Read(Demo(1)));
            Assert.AreEqual(expected.Fingerprint, complete.Fingerprint);
            Assert.AreEqual(3, complete.Rows.Count);
            Assert.AreEqual("MAT-201", source.Read(Demo(2)).Single().Registration);
            Assert.IsNull(source.Read(Demo(1, "PI-02")).Single().Grade);
            Assert.AreEqual(0, source.Read(Demo(3)).Count);
            using (var connection = new OracleConnection(Oracle))
            using (var command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = "UPDATE ACADEMIC_OWNER.COURSE_GRADE_SNAPSHOT SET GRADE=GRADE WHERE 1=0";
                Assert.IsTrue(new[] { 942, 1031 }.Contains(Assert.ThrowsException<OracleException>(() => command.ExecuteNonQuery()).Number));
            }
        }

        [TestMethod]
        public async Task ConcurrentOracleSnapshotImportAndCloseCommitOneRecordAndOneAuditPerTransition()
        {
            var snapshot = UniqueSnapshot();
            var store = new SqlClosureStore(Sql);
            var imports = await Race(() => store.Import(snapshot, "secretaria.local"));
            Assert.AreEqual(imports[0].Id, imports[1].Id);
            Assert.AreEqual(1, imports.Count(x => x.Created));
            var id = imports[0].Id;
            Assert.IsNull(store.Find(2, id));
            Assert.AreEqual("closure_not_found", Assert.ThrowsException<WorkflowException>(() => store.Close(2, id, 1, "coordenacao.local")).Code);
            var pending = store.Find(1, id);
            Assert.AreEqual("pending", pending.Status);
            Assert.AreEqual(3, pending.StudentCount);
            Assert.AreEqual(2, pending.ApprovedCount);
            Assert.AreEqual("failed", pending.Rows.Last().Outcome);
            Assert.AreEqual(1, pending.Audit.Count);
            var closes = await Race(() =>
            {
                try { return store.Close(1, id, pending.Version, "coordenacao.local").Status; }
                catch (WorkflowException error) { return error.Code; }
            });
            CollectionAssert.AreEquivalent(new[] { "closed", "closure_conflict" }, closes);
            var closed = store.Find(1, id);
            Assert.AreEqual(2, closed.Version);
            CollectionAssert.AreEqual(new[] { "imported", "closed" }, closed.Audit.Select(a => a.Action).ToArray());
            Assert.AreEqual("coordenacao.local", closed.Audit[1].Actor);
        }

        [TestMethod]
        public void AuditInsertFailureRollsBackCloseAndRuntimeCannotDeleteAudit()
        {
            var store = new SqlClosureStore(Sql);
            var id = store.Import(UniqueSnapshot(), "secretaria.local").Id;
            Assert.AreEqual(547, Assert.ThrowsException<SqlException>(() => store.Close(1, id, 1, "")).Number);
            var closure = store.Find(1, id);
            Assert.AreEqual("pending", closure.Status);
            Assert.AreEqual(1, closure.Audit.Count);
            using (var connection = new SqlConnection(Sql))
            using (var command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = "DELETE FROM dbo.ClosureAudit WHERE 1=0";
                Assert.AreEqual(229, Assert.ThrowsException<SqlException>(() => command.ExecuteNonQuery()).Number);
            }
        }

        [TestMethod]
        public void SameClassCannotOverwriteAnAlreadyImportedDifferentSnapshot()
        {
            var snapshot = UniqueSnapshot();
            var store = new SqlClosureStore(Sql);
            var imported = store.Import(snapshot, "secretaria.local");
            var modified = ClosingPolicy.Check(snapshot.Key, new[] { new AcademicRecord("MAT-101", "Lia Martins", "Projeto Integrador", 7m, 75m) });
            Assert.AreEqual("snapshot_already_exists", Assert.ThrowsException<WorkflowException>(() => store.Import(modified, "secretaria.local")).Code);
            Assert.AreEqual(3, store.Find(1, imported.Id).Rows.Count);
        }

        private static Preview UniqueSnapshot()
        {
            var key = Demo(1, "IT-" + Guid.NewGuid().ToString("N").Substring(0, 15).ToUpperInvariant());
            return ClosingPolicy.Check(key, new OracleAcademicSource(Oracle).Read(Demo(1)));
        }

        private static async Task<T[]> Race<T>(Func<T> action)
        {
            using (var start = new Barrier(2))
            {
                var jobs = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
                {
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Concurrency barrier timed out.");
                    return action();
                })).ToArray();
                return await Task.WhenAll(jobs);
            }
        }
    }
}
