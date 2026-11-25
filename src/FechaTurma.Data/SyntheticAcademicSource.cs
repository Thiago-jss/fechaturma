using System;
using System.Collections.Generic;
using FechaTurma.Core;

namespace FechaTurma.Data
{
    // Deliberate opt-in for Windows without an Oracle instance, not a fallback on failure.
    public sealed class SyntheticAcademicSource : IAcademicSource
    {
        public IReadOnlyList<AcademicRecord> Read(ClassKey key)
        {
            if (key.Term == "2026.2" && key.ClassCode == "PI-01" && key.Organization == 1)
                return new[] {
                    new AcademicRecord("MAT-101", "Lia Martins", "Projeto Integrador", 6m, 75m),
                    new AcademicRecord("MAT-102", "Caio Ramos", "Projeto Integrador", 8.5m, 94m),
                    new AcademicRecord("MAT-103", "Nina Lopes", "Projeto Integrador", 9m, 72m)
                };
            if (key.Term == "2026.2" && key.ClassCode == "PI-02" && key.Organization == 1)
                return new[] { new AcademicRecord("MAT-104", "Ivo Reis", "Laboratório de Sistemas", null, 90m) };
            if (key.Term == "2026.2" && key.ClassCode == "PI-01" && key.Organization == 2)
                return new[] { new AcademicRecord("MAT-201", "Eva Souza", "Outra coligada", 9m, 96m) };
            return Array.Empty<AcademicRecord>();
        }
    }
}
