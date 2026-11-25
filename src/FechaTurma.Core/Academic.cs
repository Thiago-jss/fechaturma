using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace FechaTurma.Core
{
    public sealed class ClassKey
    {
        private static readonly Regex TermPattern = new Regex(@"\A[0-9]{4}\.[12]\z", RegexOptions.CultureInvariant);
        internal static readonly Regex CodePattern = new Regex(@"\A[A-Z0-9][A-Z0-9-]{0,23}\z", RegexOptions.CultureInvariant);

        public ClassKey(int organization, string term, string classCode)
        {
            if (organization < 1 || organization > 999 || term == null || term.Length != 6 || !TermPattern.IsMatch(term) ||
                classCode == null || classCode.Length > 24 || !CodePattern.IsMatch(classCode))
                throw new ArgumentException("Informe coligada válida, período AAAA.1 ou AAAA.2 e código de turma em maiúsculas.");
            Organization = organization;
            Term = term;
            ClassCode = classCode;
        }

        public int Organization { get; }
        public string Term { get; }
        public string ClassCode { get; }
    }

    public sealed class AcademicRecord
    {
        public AcademicRecord(string registration, string name, string title, decimal? grade, decimal? attendance)
        {
            Registration = registration;
            Name = name;
            Title = title;
            Grade = grade;
            Attendance = attendance;
        }
        public string Registration { get; }
        public string Name { get; }
        public string Title { get; }
        public decimal? Grade { get; }
        public decimal? Attendance { get; }
    }

    public interface IAcademicSource
    {
        IReadOnlyList<AcademicRecord> Read(ClassKey key);
    }

    public sealed class SourceUnavailableException : Exception
    {
        public SourceUnavailableException(Exception cause) : base("Origem acadêmica indisponível.", cause) { }
    }

    public sealed class PreviewRow
    {
        public PreviewRow(AcademicRecord input, string outcome, IReadOnlyList<string> issues)
        {
            Registration = input.Registration;
            Name = input.Name;
            Grade = input.Grade;
            Attendance = input.Attendance;
            Outcome = outcome;
            Issues = issues;
        }
        public string Registration { get; }
        public string Name { get; }
        public decimal? Grade { get; }
        public decimal? Attendance { get; }
        public string Outcome { get; }
        public IReadOnlyList<string> Issues { get; }
    }

    public sealed class Preview
    {
        public Preview(ClassKey key, string title, IReadOnlyList<PreviewRow> rows, string fingerprint)
        {
            Key = key;
            Title = title;
            Rows = rows;
            Fingerprint = fingerprint;
        }
        public ClassKey Key { get; }
        public string Title { get; }
        public IReadOnlyList<PreviewRow> Rows { get; }
        public string Fingerprint { get; }
    }

    public static class ClosingPolicy
    {
        public const int MaxRecords = 200;
        public static Preview Check(ClassKey key, IReadOnlyList<AcademicRecord> records)
        {
            if (records == null || records.Count == 0) throw new WorkflowException("class_not_found");
            if (records.Count > MaxRecords) throw new WorkflowException("class_too_large");
            var title = records[0].Title;
            var duplicates = new HashSet<string>(records.GroupBy(r => r.Registration, StringComparer.Ordinal)
                .Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.Ordinal);
            var rows = records.OrderBy(r => r.Registration, StringComparer.Ordinal).Select(r =>
            {
                var issues = new List<string>();
                if (r.Registration == null || r.Registration.Length > 24 || !ClassKey.CodePattern.IsMatch(r.Registration)) issues.Add("invalid_registration");
                if (duplicates.Contains(r.Registration)) issues.Add("duplicate_registration");
                if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100 || r.Name.Any(char.IsControl)) issues.Add("invalid_name");
                if (string.IsNullOrWhiteSpace(title) || title.Length > 120 || title.Any(char.IsControl) || r.Title != title) issues.Add("invalid_title");
                CheckDecimal(r.Grade, 10m, "grade", issues);
                CheckDecimal(r.Attendance, 100m, "attendance", issues);
                var outcome = issues.Count != 0 ? null : r.Grade >= 6m && r.Attendance >= 75m ? "approved" : "failed";
                return new PreviewRow(r, outcome, issues.AsReadOnly());
            }).ToList().AsReadOnly();
            return new Preview(key, title, rows, rows.Any(r => r.Outcome == null) ? null : Fingerprint(key, title, rows));
        }

        private static void CheckDecimal(decimal? value, decimal max, string field, ICollection<string> issues)
        {
            if (!value.HasValue) issues.Add("missing_" + field);
            else if (value.Value < 0 || value.Value > max || decimal.Round(value.Value, 2) != value.Value) issues.Add("invalid_" + field);
        }

        private static string Fingerprint(ClassKey key, string title, IReadOnlyList<PreviewRow> rows)
        {
            using (var bytes = new MemoryStream())
            using (var canonical = new BinaryWriter(bytes))
            using (var sha = SHA256.Create())
            {
                canonical.Write("fechaturma/policy-v1");
                canonical.Write(key.Organization);
                canonical.Write(key.Term);
                canonical.Write(key.ClassCode);
                canonical.Write(title);
                canonical.Write(rows.Count);
                foreach (var row in rows)
                {
                    canonical.Write(row.Registration);
                    canonical.Write(row.Name);
                    canonical.Write(row.Grade.Value.ToString("F2", CultureInfo.InvariantCulture));
                    canonical.Write(row.Attendance.Value.ToString("F2", CultureInfo.InvariantCulture));
                    canonical.Write(row.Outcome);
                }
                canonical.Flush();
                bytes.Position = 0;
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }
        }
    }
}
