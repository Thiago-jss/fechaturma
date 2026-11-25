using System.Runtime.Serialization;

namespace FechaTurma.Desktop.Api
{
    [DataContract]
    internal class ClosureSummary
    {
        [DataMember(Name = "id", IsRequired = true)]
        public string Id { get; set; }

        [DataMember(Name = "organization", IsRequired = true)]
        public long Organization { get; set; }

        [DataMember(Name = "term", IsRequired = true)]
        public string Term { get; set; }

        [DataMember(Name = "classCode", IsRequired = true)]
        public string ClassCode { get; set; }

        [DataMember(Name = "title", IsRequired = true)]
        public string Title { get; set; }

        [DataMember(Name = "status", IsRequired = true)]
        public string Status { get; set; }

        [DataMember(Name = "version", IsRequired = true)]
        public int Version { get; set; }

        [DataMember(Name = "studentCount", IsRequired = true)]
        public int StudentCount { get; set; }

        [DataMember(Name = "approvedCount", IsRequired = true)]
        public int ApprovedCount { get; set; }

        [DataMember(Name = "failedCount", IsRequired = true)]
        public int FailedCount { get; set; }

        [DataMember(Name = "importedAt", IsRequired = true)]
        public string ImportedAt { get; set; }

        [DataMember(Name = "closedAt", IsRequired = true)]
        public string ClosedAt { get; set; }
    }

    [DataContract]
    internal sealed class ClosureDetail : ClosureSummary
    {
        [DataMember(Name = "rows", IsRequired = true)]
        public StudentResult[] Rows { get; set; }

        [DataMember(Name = "audit", IsRequired = true)]
        public AuditEntry[] Audit { get; set; }
    }

    [DataContract]
    internal sealed class StudentResult
    {
        [DataMember(Name = "registration", IsRequired = true)]
        public string Registration { get; set; }

        [DataMember(Name = "name", IsRequired = true)]
        public string Name { get; set; }

        [DataMember(Name = "grade", IsRequired = true)]
        public decimal Grade { get; set; }

        [DataMember(Name = "attendance", IsRequired = true)]
        public decimal Attendance { get; set; }

        [DataMember(Name = "outcome", IsRequired = true)]
        public string Outcome { get; set; }
    }

    [DataContract]
    internal sealed class AuditEntry
    {
        [DataMember(Name = "action", IsRequired = true)]
        public string Action { get; set; }

        [DataMember(Name = "actor", IsRequired = true)]
        public string Actor { get; set; }

        [DataMember(Name = "at", IsRequired = true)]
        public string At { get; set; }
    }

    [DataContract]
    internal sealed class CloseRequest
    {
        [DataMember(Name = "version", IsRequired = true)]
        public int Version { get; set; }
    }
}
