using System;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using FechaTurma.Core;

namespace FechaTurma.Web
{
    public class ImportPage : Page
    {
        protected TextBox TermInput, ClassInput, RegistrarKey;
        protected Panel MessagePanel, PreviewPanel, CommitPanel;
        protected Literal MessageText, PreviewHeading, PreviewStatus;
        protected HiddenField Fingerprint;
        protected Repeater PreviewRows;

        protected void PreviewClick(object sender, EventArgs args) => Run(() => Show(Workflow().Preview(Key())));

        protected void ImportClick(object sender, EventArgs args)
        {
            var role = Lab.Role(RegistrarKey.Text);
            RegistrarKey.Text = "";
            if (role != Lab.Registrar)
            {
                Notice("Uma chave válida da secretaria é necessária. Gere a prévia e tente novamente.", role == null ? 401 : 403);
                return;
            }
            Run(() =>
            {
                var result = Workflow().Import(Key(), Fingerprint.Value, role);
                Notice(result.Created ? "Turma importada. A coordenação já pode conferir no aplicativo de desktop."
                    : "Esse mesmo retrato já foi importado. Nenhum registro foi duplicado.", 200);
            });
        }

        private ClassKey Key()
        {
            try { return new ClassKey(Lab.Organization, TermInput.Text.Trim(), ClassInput.Text.Trim().ToUpperInvariant()); }
            catch (ArgumentException) { throw new WorkflowException("invalid_class_key"); }
        }
        private static ClosingWorkflow Workflow() => Lab.Workflow();

        private void Show(Preview preview)
        {
            var valid = preview.Fingerprint != null;
            PreviewPanel.Visible = true;
            PreviewHeading.Text = (string.IsNullOrWhiteSpace(preview.Title) ? "Prévia" : preview.Title) + " · " + preview.Key.ClassCode;
            PreviewStatus.Text = valid ? "Prévia completa. Confira os resultados antes de enviar."
                : "Importação bloqueada. Corrija todas as pendências na origem e gere uma nova prévia.";
            if (!valid) Response.StatusCode = 422;
            CommitPanel.Visible = valid;
            Fingerprint.Value = preview.Fingerprint ?? "";
            PreviewRows.DataSource = preview.Rows.Select(row => new
            {
                row.Registration,
                row.Name,
                Grade = row.Grade?.ToString("N2", CultureInfo.CurrentCulture) ?? "—",
                Attendance = row.Attendance.HasValue ? row.Attendance.Value.ToString("N2", CultureInfo.CurrentCulture) + "%" : "—",
                Pending = row.Issues.Count > 0 ? "true" : "false",
                Result = row.Issues.Count > 0 ? string.Join(" · ", row.Issues.Select(Lab.IssueText)) : (row.Outcome == "approved" ? "Aprovado" : "Reprovado")
            }).ToArray();
            PreviewRows.DataBind();
        }

        private void Run(Action work)
        {
            try { work(); }
            catch (WorkflowException err)
            {
                var status = err.Code == "invalid_class_key" ? 400 : err.Code == "class_not_found" ? 404 : err.Code == "invalid_snapshot" || err.Code == "class_too_large" ? 422 : 409;
                Notice(Lab.ErrorText(err.Code), status);
            }
            catch (DbException error) { Unavailable(error); }
            catch (SourceUnavailableException error) { Unavailable(error); }
            catch (InvalidOperationException error) { Unavailable(error); }
            catch (ArgumentException error) { Unavailable(error); }
        }

        private void Unavailable(Exception error)
        {
            Lab.LogFailure(error);
            Notice("Origem ou serviço indisponível. Nenhum resultado foi confirmado; tente novamente.", 503);
        }

        private void Notice(string message, int status)
        {
            MessageText.Text = message;
            MessagePanel.Visible = true;
            MessagePanel.CssClass = status >= 400 ? "notice warning" : "notice success";
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}
