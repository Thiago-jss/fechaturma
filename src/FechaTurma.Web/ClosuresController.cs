using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Web.Http;
using FechaTurma.Core;
using Newtonsoft.Json.Linq;

namespace FechaTurma.Web
{
    [RoutePrefix("api/v1")]
    public sealed class ClosuresController : ApiController
    {
        [HttpGet, Route("health")]
        public IHttpActionResult Health() => Ok(new { status = "ok" });

        [HttpGet, Route("closures")]
        public IHttpActionResult List() => WithStore(store => Ok(store.List(Lab.Organization)));

        [HttpGet, Route("closures/{id:guid}")]
        public IHttpActionResult Detail(Guid id) => WithStore(store =>
        {
            var closure = store.Find(Lab.Organization, id);
            return closure == null ? Problem(HttpStatusCode.NotFound, "closure_not_found", Lab.ErrorText("closure_not_found")) : Ok(closure);
        });

        [HttpPost, Route("closures/{id:guid}/close")]
        public IHttpActionResult Close(Guid id, [FromBody] JObject body)
        {
            var header = Request.Headers.Authorization;
            var role = header != null && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                ? Lab.Role(header.Parameter) : null;
            if (role == null) return Problem(HttpStatusCode.Unauthorized, "unauthorized", "Informe uma chave válida.");
            if (role != Lab.Coordinator) return Problem(HttpStatusCode.Forbidden, "forbidden", "Somente a coordenação pode fechar uma turma.");
            var version = body?["version"];
            if (!ModelState.IsValid || body == null || body.Properties().Count() != 1 || version == null ||
                version.Type != JTokenType.Integer || !int.TryParse(version.ToString(), out var expectedVersion) || expectedVersion < 1)
                return Problem(HttpStatusCode.BadRequest, "invalid_version", "Informe apenas a versão positiva observada no detalhe.");
            return WithStore(store => Ok(store.Close(Lab.Organization, id, expectedVersion, role)));
        }

        private IHttpActionResult WithStore(Func<IClosureStore, IHttpActionResult> operation)
        {
            try { return operation(Lab.Store()); }
            catch (WorkflowException error)
            {
                return Problem(error.Code == "closure_not_found" ? HttpStatusCode.NotFound : HttpStatusCode.Conflict,
                    error.Code, Lab.ErrorText(error.Code));
            }
            catch (DbException error) { return Unavailable(error); }
            catch (InvalidOperationException error) { return Unavailable(error); }
        }

        private IHttpActionResult Unavailable(Exception error)
        {
            Lab.LogFailure(error);
            return Problem(HttpStatusCode.ServiceUnavailable, "unavailable", "Serviço temporariamente indisponível.");
        }

        private IHttpActionResult Problem(HttpStatusCode status, string code, string message) => Content(status, new { code, message });
    }
}
