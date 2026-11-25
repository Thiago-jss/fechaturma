using System;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using FechaTurma.Core;
using FechaTurma.Data;
using Newtonsoft.Json.Serialization;

namespace FechaTurma.Web
{
    public class WebApplication : HttpApplication
    {
        protected void Application_Start()
        {
            MvcHandler.DisableMvcResponseHeader = true;
            GlobalConfiguration.Configure(config =>
            {
                config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Never;
                config.Formatters.Remove(config.Formatters.XmlFormatter);
                config.Formatters.JsonFormatter.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                config.Formatters.JsonFormatter.SerializerSettings.MaxDepth = 16;
                config.MapHttpAttributeRoutes();
            });
            RouteTable.Routes.MapRoute("home", "", new { controller = "Site", action = "Index" });
            RouteTable.Routes.MapRoute("report", "atas/{id}", new { controller = "Site", action = "Report" });
        }

        protected void Application_BeginRequest()
        {
            var request = Context.Request;
            var host = request.Url.Host;
            var allowedHost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1";
            if (!request.IsLocal || !allowedHost)
            {
                Context.Response.StatusCode = 404;
                Context.Response.SuppressContent = true;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!request.Path.StartsWith("/painel/", StringComparison.OrdinalIgnoreCase) &&
                !request.Path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
                Context.Response.Cache.SetNoStore();
        }
    }

    internal static class Lab
    {
        public const int Organization = 1;
        public const string Registrar = "secretaria.local";
        public const string Coordinator = "coordenacao.local";
        public static IClosureStore Store() => new SqlClosureStore(Required("FT_SQL_CONNECTION"));
        public static ClosingWorkflow Workflow()
        {
            var mode = Environment.GetEnvironmentVariable("FT_ACADEMIC_SOURCE");
            IAcademicSource source;
            if (mode == "fixture") source = new SyntheticAcademicSource();
            else if (mode == "oracle") source = new OracleAcademicSource(Required("FT_ORACLE_CONNECTION"));
            else throw new InvalidOperationException("Selecione explicitamente a origem acadêmica do laboratório.");
            return new ClosingWorkflow(source, Store());
        }

        public static string Required(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Configuração ausente: " + name);
            return value;
        }

        public static string Role(string presented)
        {
            var registrar = Environment.GetEnvironmentVariable("FT_IMPORT_KEY");
            var coordinator = Environment.GetEnvironmentVariable("FT_DESKTOP_KEY");
            if (registrar == null || coordinator == null || registrar.Length < 32 || coordinator.Length < 32 || registrar == coordinator)
                return null;
            if (Equal(presented, registrar)) return Registrar;
            return Equal(presented, coordinator) ? Coordinator : null;
        }

        private static bool Equal(string actual, string expected)
        {
            if (actual == null || actual.Length != expected.Length) return false;
            var difference = 0;
            for (var i = 0; i < expected.Length; i++) difference |= actual[i] ^ expected[i];
            return difference == 0;
        }

        public static void LogFailure(Exception error) => Trace.TraceError("FechaTurma dependency failure: {0}", error.GetType().Name);

        public static string ErrorText(string code)
        {
            switch (code)
            {
                case "invalid_class_key": return "Informe um período no formato AAAA.1 ou AAAA.2 e um código de turma válido.";
                case "class_not_found": return "Turma não encontrada na origem acadêmica.";
                case "class_too_large": return "A turma excede o limite de 200 registros deste laboratório.";
                case "invalid_snapshot": return "Corrija as pendências na origem antes de importar.";
                case "stale_preview": return "A origem mudou. Gere uma nova prévia antes de importar.";
                case "snapshot_already_exists": return "Já existe um retrato diferente desta turma e período.";
                case "closure_not_found": return "Fechamento não encontrado.";
                case "closure_conflict": return "A turma já foi fechada ou sua versão mudou. Atualize a lista.";
                default: return "Não foi possível concluir a operação.";
            }
        }

        public static string IssueText(string code)
        {
            switch (code)
            {
                case "missing_grade": return "Nota ausente";
                case "missing_attendance": return "Frequência ausente";
                case "invalid_grade": return "Nota fora da faixa ou com mais de duas casas";
                case "invalid_attendance": return "Frequência fora da faixa ou com mais de duas casas";
                case "duplicate_registration": return "Matrícula duplicada";
                case "invalid_registration": return "Matrícula inválida";
                case "invalid_name": return "Nome inválido";
                case "invalid_title": return "Disciplina inconsistente";
                default: return "Registro inválido";
            }
        }
    }
}
