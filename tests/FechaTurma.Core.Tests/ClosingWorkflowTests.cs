using System.Collections.Generic;
using FechaTurma.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FechaTurma.Tests
{
    [TestClass]
    public sealed class ClosingWorkflowTests
    {
        [TestMethod]
        public void SourceChangeAfterPreviewCannotBeImportedWithoutAnotherReview()
        {
            var source = new MutableSource { Grade = 6m };
            // A null store deliberately fails if a stale command passes the boundary.
            var workflow = new ClosingWorkflow(source, null);
            var key = new ClassKey(1, "2026.2", "PI-01");
            var approved = workflow.Preview(key);
            source.Grade = 7m;
            var failure = Assert.ThrowsException<WorkflowException>(() => workflow.Import(key, approved.Fingerprint, "secretaria.local"));
            Assert.AreEqual("stale_preview", failure.Code);
        }

        private sealed class MutableSource : IAcademicSource
        {
            public decimal Grade { get; set; }
            public IReadOnlyList<AcademicRecord> Read(ClassKey key) =>
                new[] { new AcademicRecord("MAT-101", "Lia Martins", "Projeto Integrador", Grade, 75m) };
        }
    }
}
