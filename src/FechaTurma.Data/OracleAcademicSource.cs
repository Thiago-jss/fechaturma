using System.Collections.Generic;
using FechaTurma.Core;
using Oracle.ManagedDataAccess.Client;

namespace FechaTurma.Data
{
    public sealed class OracleAcademicSource : IAcademicSource
    {
        private readonly string connectionString;
        public OracleAcademicSource(string connectionString) { this.connectionString = connectionString; }

        public IReadOnlyList<AcademicRecord> Read(ClassKey key)
        {
            try
            {
                using (var connection = new OracleConnection(connectionString))
                using (var command = connection.CreateCommand())
                {
                    command.BindByName = true;
                    command.CommandTimeout = 8;
                    command.CommandText = @"SELECT REGISTRATION, STUDENT_NAME, CLASS_TITLE, GRADE, ATTENDANCE
FROM ACADEMIC_OWNER.COURSE_GRADE_SNAPSHOT
WHERE ORGANIZATION = :organization AND TERM = :term AND CLASS_CODE = :classCode
ORDER BY REGISTRATION FETCH FIRST 201 ROWS ONLY";
                    command.Parameters.Add("organization", OracleDbType.Int32).Value = key.Organization;
                    command.Parameters.Add("term", OracleDbType.Varchar2, 6).Value = key.Term;
                    command.Parameters.Add("classCode", OracleDbType.Varchar2, 24).Value = key.ClassCode;
                    connection.Open();
                    var records = new List<AcademicRecord>();
                    using (var reader = command.ExecuteReader())
                        while (reader.Read())
                            records.Add(new AcademicRecord(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                                reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3), reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4)));
                    return records.AsReadOnly();
                }
            }
            catch (OracleException ex) { throw new SourceUnavailableException(ex); }
        }
    }
}
