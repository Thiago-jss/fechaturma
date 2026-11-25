using System;
using System.Collections.Generic;

namespace FechaTurma.Core
{
    public sealed class WorkflowException : Exception
    {
        public WorkflowException(string code) : base(code) { Code = code; }
        public string Code { get; }
    }

    public class ClosureSummary
    {
        public ClosureSummary(Guid id, ClassKey key, string title, int version, int studentCount, int approvedCount, DateTimeOffset importedAt, DateTimeOffset? closedAt)
        {
            Id = id; Organization = key.Organization; Term = key.Term; ClassCode = key.ClassCode; Title = title;
            Version = version; StudentCount = studentCount; ApprovedCount = approvedCount; ImportedAt = importedAt; ClosedAt = closedAt;
        }
        public Guid Id { get; }
        public int Organization { get; }
        public string Term { get; }
        public string ClassCode { get; }
        public string Title { get; }
        public string Status => ClosedAt.HasValue ? "closed" : "pending";
        public int Version { get; }
        public int StudentCount { get; }
        public int ApprovedCount { get; }
        public int FailedCount => StudentCount - ApprovedCount;
        public DateTimeOffset ImportedAt { get; }
        public DateTimeOffset? ClosedAt { get; }
    }

    public sealed class Closure : ClosureSummary
    {
        public Closure(ClosureSummary summary, IReadOnlyList<ClosedRow> rows, IReadOnlyList<AuditEntry> audit)
            : base(summary.Id, new ClassKey(summary.Organization, summary.Term, summary.ClassCode), summary.Title, summary.Version,
                  summary.StudentCount, summary.ApprovedCount, summary.ImportedAt, summary.ClosedAt)
        { Rows = rows; Audit = audit; }
        public IReadOnlyList<ClosedRow> Rows { get; }
        public IReadOnlyList<AuditEntry> Audit { get; }
    }

    public sealed class ClosedRow
    {
        public ClosedRow(string registration, string name, decimal grade, decimal attendance, string outcome)
        { Registration = registration; Name = name; Grade = grade; Attendance = attendance; Outcome = outcome; }
        public string Registration { get; }
        public string Name { get; }
        public decimal Grade { get; }
        public decimal Attendance { get; }
        public string Outcome { get; }
    }

    public sealed class AuditEntry
    {
        public AuditEntry(string action, string actor, DateTimeOffset at) { Action = action; Actor = actor; At = at; }
        public string Action { get; }
        public string Actor { get; }
        public DateTimeOffset At { get; }
    }

    public sealed class ImportResult
    {
        public ImportResult(Guid id, bool created) { Id = id; Created = created; }
        public Guid Id { get; }
        public bool Created { get; }
    }

    public interface IClosureStore
    {
        ImportResult Import(Preview snapshot, string actor);
        IReadOnlyList<ClosureSummary> List(int organization);
        Closure Find(int organization, Guid id);
        Closure Close(int organization, Guid id, int expectedVersion, string actor);
    }

    public sealed class ClosingWorkflow
    {
        private readonly IAcademicSource source;
        private readonly IClosureStore store;
        public ClosingWorkflow(IAcademicSource source, IClosureStore store) { this.source = source; this.store = store; }
        public Preview Preview(ClassKey key) => ClosingPolicy.Check(key, source.Read(key));
        public ImportResult Import(ClassKey key, string fingerprint, string actor)
        {
            var snapshot = Preview(key);
            if (snapshot.Fingerprint == null) throw new WorkflowException("invalid_snapshot");
            if (!string.Equals(snapshot.Fingerprint, fingerprint, StringComparison.Ordinal)) throw new WorkflowException("stale_preview");
            return store.Import(snapshot, actor);
        }
    }
}
