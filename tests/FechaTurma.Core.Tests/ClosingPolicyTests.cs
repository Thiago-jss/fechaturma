using System.Linq;
using FechaTurma.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace FechaTurma.Tests
{
    [TestClass]
    public sealed class ClosingPolicyTests
    {
        private static readonly ClassKey Key = new ClassKey(1, "2026.2", "PI-01");

        [TestMethod]
        public void InclusiveThresholdsApproveButEitherDeficitFails()
        {
            var result = ClosingPolicy.Check(Key, new[] { Record("A", 6m, 75m), Record("B", 10m, 74.99m), Record("C", 5.99m, 100m) });
            CollectionAssert.AreEqual(new[] { "approved", "failed", "failed" }, result.Rows.Select(r => r.Outcome).ToArray());
            Assert.IsNotNull(result.Fingerprint);
        }

        [TestMethod]
        public void MissingGradeAndDuplicateRegistrationBlockWholePreview()
        {
            var result = ClosingPolicy.Check(Key, new[] { Record("A", null, 80m), Record("A", 6m, 80m) });
            Assert.IsNull(result.Fingerprint);
            Assert.IsTrue(result.Rows.All(r => r.Issues.Contains("duplicate_registration")));
            Assert.IsTrue(result.Rows.Any(r => r.Issues.Contains("missing_grade")));
        }

        [TestMethod]
        public void ReorderedAndEquivalentDecimalsYieldSameSnapshotButChangedAttendanceDoesNot()
        {
            var a = ClosingPolicy.Check(Key, new[] { Record("A", 6m, 75m), Record("B", 7m, 90m) });
            var b = ClosingPolicy.Check(Key, new[] { Record("B", 7.00m, 90m), Record("A", 6m, 75m) });
            var c = ClosingPolicy.Check(Key, new[] { Record("B", 7m, 91m), Record("A", 6m, 75m) });
            Assert.AreEqual(a.Fingerprint, b.Fingerprint);
            Assert.AreNotEqual(a.Fingerprint, c.Fingerprint);
        }

        [TestMethod]
        public void OutOfRangeAndExcessPrecisionAreNotRoundedIntoValidRecords()
        {
            var result = ClosingPolicy.Check(Key, new[] { Record("A", 5.999m, 105m) });
            Assert.IsNull(result.Fingerprint);
            CollectionAssert.AreEquivalent(new[] { "invalid_grade", "invalid_attendance" }, result.Rows.Single().Issues.ToArray());
        }

        private static AcademicRecord Record(string id, decimal? grade, decimal? attendance) =>
            new AcademicRecord(id, "Aluno " + id, "Projeto Integrador", grade, attendance);
    }
}
