using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using FechaTurma.Desktop.Api;

namespace FechaTurma.Desktop
{
    internal sealed class CoordinatorForm : Form
    {
        private static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
        private readonly ClosureClient client;
        private readonly TextBox key = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill, AccessibleName = "Chave de acesso" };
        private readonly Button refresh = new Button { Text = "&Atualizar lista", Dock = DockStyle.Fill };
        private readonly ComboBox classes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Turmas pendentes" };
        private readonly Button close = new Button { Text = "&Fechar turma", Dock = DockStyle.Fill };
        private readonly Label summary = TextLine("Nenhuma turma selecionada.");
        private readonly Label status = TextLine("Informe a chave e selecione Atualizar lista.");
        private readonly DataGridView students = Grid("Resultados dos estudantes");
        private readonly DataGridView audit = Grid("Histórico de operações");
        private ClosureDetail current;
        private bool busy;
        private Task operation = Task.CompletedTask;
        private Func<ClosureDetail, bool> confirmClose;

        public CoordinatorForm(ClosureClient client, string initialKey)
        {
            this.client = client;
            confirmClose = ShowConfirmation;
            Text = "FechaTurma — Coordenação";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(700, 520);
            ClientSize = new Size(900, 620);
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            key.Text = initialKey ?? "";
            key.TabIndex = 1;
            refresh.TabIndex = 2;
            classes.TabIndex = 4;
            close.TabIndex = 6;
            ConfigureGrids();

            var layout = new TableLayoutPanel { ColumnCount = 3, RowCount = 7, Dock = DockStyle.Fill, Padding = new Padding(18) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            foreach (var height in new[] { 56, 36, 36, 56 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            var heading = TextLine("Fechamento acadêmico\r\nConfira os resultados antes de fechar a turma.");
            heading.Font = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold);
            AddWide(layout, heading, 0);
            layout.Controls.Add(new Label { Text = "&Chave de acesso", AutoSize = true, Anchor = AnchorStyles.Left, TabIndex = 0 }, 0, 1);
            layout.Controls.Add(key, 1, 1);
            layout.Controls.Add(refresh, 2, 1);
            layout.Controls.Add(new Label { Text = "&Turmas pendentes", AutoSize = true, Anchor = AnchorStyles.Left, TabIndex = 3 }, 0, 2);
            layout.Controls.Add(classes, 1, 2);
            layout.SetColumnSpan(classes, 2);
            AddWide(layout, summary, 3);
            var tabs = new TabControl { Dock = DockStyle.Fill, TabIndex = 5 };
            var resultsTab = new TabPage("Resultados");
            resultsTab.Controls.Add(students);
            tabs.TabPages.Add(resultsTab);
            var auditTab = new TabPage("Histórico");
            auditTab.Controls.Add(audit);
            tabs.TabPages.Add(auditTab);
            AddWide(layout, tabs, 4);
            layout.Controls.Add(close, 2, 5);
            AddWide(layout, status, 6);
            Controls.Add(layout);

            refresh.Click += (_, __) => Start(RefreshAsync);
            classes.SelectedIndexChanged += (_, __) =>
            {
                if (!busy && classes.SelectedItem is ClassChoice choice)
                    Start(() => LoadDetailAsync(choice.Id));
            };
            close.Click += (_, __) =>
            {
                if (!busy && current != null && current.Status == "pending")
                {
                    var reviewed = current;
                    if (confirmClose(reviewed)) Start(() => CloseAsync(reviewed), closing: true);
                }
            };
            key.TextChanged += (_, __) =>
            {
                if (busy) return;
                classes.Items.Clear();
                ClearDetail();
                status.Text = "Chave alterada. Atualize a lista para continuar.";
                UpdateActions();
            };
            FormClosing += (_, args) =>
            {
                if (busy && args.CloseReason == CloseReason.UserClosing) args.Cancel = true;
            };
            UpdateActions();
        }

        // Operate the same buttons and selection handler as an operator.
        internal async Task<int> RunSmokeAsync(Guid? closeTarget)
        {
            if (!key.UseSystemPasswordChar || close.Enabled) return 1;
            refresh.PerformClick();
            await operation;
            var choice = classes.Items.Cast<ClassChoice>().FirstOrDefault(item => !closeTarget.HasValue || item.Id == closeTarget.Value);
            if (choice == null) return 1;
            classes.SelectedItem = choice;
            await operation;
            if (current == null || Guid.Parse(current.Id) != choice.Id || current.Status != "pending" || !close.Enabled ||
                students.Rows.Count != current.Rows.Length || audit.Rows.Count != current.Audit.Length) return 1;

            var reviewed = current;
            var sawConfirmation = false;
            confirmClose = detail =>
            {
                sawConfirmation = ReferenceEquals(detail, reviewed);
                return false;
            };
            close.PerformClick();
            if (!sawConfirmation || !ReferenceEquals(current, reviewed) || !close.Enabled) return 1;
            if (!closeTarget.HasValue) return 0;

            confirmClose = detail => Guid.Parse(detail.Id) == closeTarget.Value;
            close.PerformClick();
            await operation;
            if (current == null || Guid.Parse(current.Id) != closeTarget.Value || current.Status != "closed" || close.Enabled ||
                students.Rows.Count != current.Rows.Length || audit.Rows.Count != current.Audit.Length ||
                classes.Items.Cast<ClassChoice>().Any(item => item.Id == closeTarget.Value)) return 1;
            var persisted = await client.DetailAsync(closeTarget.Value, key.Text.Trim());
            return persisted.Status == "closed" && persisted.Version == current.Version && persisted.Audit.Any(entry => entry.Action == "closed") ? 0 : 1;
        }

        private void Start(Func<Task> action, bool closing = false)
        {
            if (!busy) operation = ExecuteAsync(action, closing);
        }

        private async Task ExecuteAsync(Func<Task> action, bool closing)
        {
            busy = true;
            UpdateActions();
            status.Text = closing ? "Aguardando confirmação do servidor…" : "Consultando o servidor local…";
            var completed = false;
            try
            {
                await action();
                completed = true;
            }
            catch (ClosureHttpException error)
            {
                status.Text = DescribeHttp(error.Status, closing);
            }
            catch (OperationCanceledException)
            {
                status.Text = closing ? "Tempo esgotado. Atualize a lista para verificar se a turma foi fechada." : "Tempo esgotado. Atualize a lista para tentar novamente.";
            }
            catch (HttpRequestException)
            {
                status.Text = closing ? "Sem resposta do servidor. Atualize a lista para verificar o resultado." : "Não foi possível acessar o servidor local. Verifique o serviço e atualize a lista.";
            }
            catch (Exception error) when (error is SerializationException || error is XmlException || error is FormatException)
            {
                status.Text = closing ? "Resposta inválida. Atualize a lista para verificar o resultado." : "Resposta inválida do servidor ou chave em formato inválido. Revise a chave e atualize a lista.";
            }
            finally
            {
                if (!completed)
                {
                    classes.Items.Clear();
                    ClearDetail();
                }
                busy = false;
                UpdateActions();
            }
        }

        private async Task RefreshAsync()
        {
            classes.Items.Clear();
            ClearDetail();
            var list = await client.ListAsync(key.Text.Trim());
            foreach (var item in list.Where(item => item.Status == "pending")) classes.Items.Add(new ClassChoice(item));
            status.Text = classes.Items.Count == 0 ? "Nenhuma turma pendente." : "Selecione uma turma para conferir os resultados.";
        }

        private async Task LoadDetailAsync(Guid id)
        {
            ClearDetail();
            var detail = await client.DetailAsync(id, key.Text.Trim());
            Render(detail);
            if (detail.Status == "closed")
            {
                RemoveChoice(id);
                status.Text = "Esta turma já está fechada no servidor.";
            }
            else status.Text = "Confira os resultados e, se estiverem corretos, selecione Fechar turma.";
        }

        private async Task CloseAsync(ClosureDetail reviewed)
        {
            var closed = await client.CloseAsync(Guid.Parse(reviewed.Id), reviewed.Version, key.Text.Trim());
            Render(closed);
            RemoveChoice(Guid.Parse(closed.Id));
            status.Text = "Turma fechada com sucesso. O histórico foi atualizado pelo servidor.";
        }

        private void RemoveChoice(Guid id)
        {
            var choice = classes.Items.Cast<ClassChoice>().FirstOrDefault(item => item.Id == id);
            if (choice != null) classes.Items.Remove(choice);
            classes.SelectedIndex = -1;
        }

        private bool ShowConfirmation(ClosureDetail reviewed) => MessageBox.Show(this,
            $"Fechar a turma {reviewed.ClassCode} — {reviewed.Title} ({reviewed.Term})?\r\n\r\n" +
            $"{reviewed.StudentCount} estudante(s), {reviewed.ApprovedCount} aprovado(s), {reviewed.FailedCount} reprovado(s).\r\n" +
            "O fechamento não pode ser desfeito por este aplicativo.", "Confirmar fechamento", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        private void Render(ClosureDetail detail)
        {
            ClearDetail();
            current = detail;
            var state = detail.Status == "pending" ? "Pendente" : "Fechada";
            summary.Text = $"{detail.ClassCode} — {detail.Title}  |  Período: {detail.Term}  |  Org.: {detail.Organization}\r\n" +
                $"{state} · Versão {detail.Version} · {detail.StudentCount} estudante(s) · {detail.ApprovedCount} aprovado(s) · {detail.FailedCount} reprovado(s)";
            foreach (var row in detail.Rows)
                students.Rows.Add(row.Registration, row.Name, row.Grade.ToString("0.##", Brazilian), row.Attendance.ToString("0.##", Brazilian),
                    row.Outcome == "approved" ? "Aprovado" : row.Outcome == "failed" ? "Reprovado" : "Não reconhecido");
            foreach (var entry in detail.Audit)
                audit.Rows.Add(entry.Action == "imported" ? "Importação" : entry.Action == "closed" ? "Fechamento" : "Não reconhecida", entry.Actor,
                    DateTimeOffset.TryParse(entry.At, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at.ToLocalTime().ToString("g", Brazilian) : "Não disponível");
        }

        private void ClearDetail()
        {
            current = null;
            summary.Text = "Nenhuma turma selecionada.";
            students.Rows.Clear();
            audit.Rows.Clear();
        }

        private void UpdateActions()
        {
            key.Enabled = !busy;
            refresh.Enabled = !busy;
            classes.Enabled = !busy && classes.Items.Count > 0;
            close.Enabled = !busy && current != null && current.Status == "pending";
        }

        private static string DescribeHttp(HttpStatusCode code, bool closing)
        {
            switch (code)
            {
                case HttpStatusCode.Unauthorized: return "401 — Chave de acesso ausente ou inválida. Revise a chave e atualize a lista.";
                case HttpStatusCode.Forbidden: return "403 — A chave não tem a permissão necessária. Troque a chave e atualize a lista.";
                case HttpStatusCode.NotFound: return "404 — Turma ou serviço não encontrado. Atualize a lista.";
                case HttpStatusCode.Conflict: return "409 — A turma mudou ou já foi fechada. Atualize a lista e confira o estado atual.";
                default: return $"HTTP {(int)code} — Resposta inesperada. " + (closing ? "Atualize a lista para verificar o resultado." : "Atualize a lista para tentar novamente.");
            }
        }

        private static Label TextLine(string text) => new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, UseMnemonic = false };

        private static void AddWide(TableLayoutPanel layout, Control control, int row)
        {
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 3);
        }

        private static DataGridView Grid(string name) => new DataGridView
        {
            AccessibleName = name, Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, MultiSelect = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable
        };

        private void ConfigureGrids()
        {
            students.Columns.Add("registration", "Matrícula");
            students.Columns.Add("name", "Estudante");
            students.Columns["name"].FillWeight = 200;
            students.Columns.Add("grade", "Nota");
            students.Columns.Add("attendance", "Frequência (%)");
            students.Columns.Add("outcome", "Resultado");
            audit.Columns.Add("action", "Operação");
            audit.Columns.Add("actor", "Responsável");
            audit.Columns.Add("at", "Data e hora local");
            foreach (var grid in new[] { students, audit })
                foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private sealed class ClassChoice
        {
            private readonly ClosureSummary item;
            public ClassChoice(ClosureSummary item) { this.item = item; }
            public Guid Id => Guid.Parse(item.Id);
            public override string ToString() => $"{item.ClassCode} — {item.Title}  |  {item.Term}  |  Org. {item.Organization}";
        }
    }
}
