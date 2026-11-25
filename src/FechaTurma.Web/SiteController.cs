using System;
using System.Data.Common;
using System.Web.Mvc;

namespace FechaTurma.Web
{
    public sealed class SiteController : Controller
    {
        [HttpGet]
        public ActionResult Index() => View();

        [HttpGet]
        public ActionResult Report(Guid? id)
        {
            if (!id.HasValue) return HttpNotFound();
            try
            {
                var closure = Lab.Store().Find(Lab.Organization, id.Value);
                return closure != null && closure.ClosedAt.HasValue ? (ActionResult)View(closure) : HttpNotFound();
            }
            catch (DbException error) { return Unavailable(error); }
            catch (InvalidOperationException error) { return Unavailable(error); }
        }

        private static ActionResult Unavailable(Exception error)
        {
            Lab.LogFailure(error);
            return new HttpStatusCodeResult(503, "Servico temporariamente indisponivel.");
        }
    }
}
