<%@ Page Language="C#" Inherits="FechaTurma.Web.ImportPage" EnableViewState="false" %>
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Importação acadêmica — FechaTurma</title><link rel="stylesheet" href="/assets/site.css">
  <script defer src="/assets/jquery.min.js"></script><script defer src="/assets/importacao.js"></script>
</head>
<body>
<main class="container">
  <nav class="nav" aria-label="Navegação"><a href="/">← Início</a><a href="/painel/">Painel de fechamentos →</a></nav>
  <p class="eyebrow">01 · SECRETARIA</p><h1>Importar uma turma</h1>
  <p class="lead">Carregue as notas e a frequência. Pendências devem ser corrigidas na origem; nenhum registro parcial é importado.</p>
  <form id="ImportForm" runat="server">
    <fieldset>
      <legend>Selecionar turma</legend>
      <div class="fields">
        <div><asp:Label AssociatedControlID="TermInput" runat="server">Período letivo</asp:Label><asp:TextBox ID="TermInput" MaxLength="6" Text="2026.2" runat="server" /></div>
        <div><asp:Label AssociatedControlID="ClassInput" runat="server">Código da turma</asp:Label><asp:TextBox ID="ClassInput" MaxLength="24" Text="PI-01" runat="server" /></div>
        <asp:Button ID="PreviewButton" Text="Gerar prévia" OnClick="PreviewClick" CssClass="button secondary" runat="server" />
      </div>
      <p class="hint">Coligada 1 · Experimente <b>PI-01</b> (completa) ou <b>PI-02</b> (com pendência).</p>
    </fieldset>
    <asp:Panel ID="MessagePanel" Visible="false" CssClass="notice" role="status" runat="server"><asp:Literal ID="MessageText" Mode="Encode" runat="server" /></asp:Panel>
    <asp:Panel ID="PreviewPanel" Visible="false" CssClass="preview" runat="server">
      <h2><asp:Literal ID="PreviewHeading" Mode="Encode" runat="server" /></h2>
      <p><asp:Literal ID="PreviewStatus" Mode="Encode" runat="server" /></p>
      <fieldset data-filter-panel hidden><legend>Filtrar prévia</legend><label><input type="checkbox" data-pending-filter> Mostrar apenas matrículas com pendências</label><p data-visible-count aria-live="polite"></p></fieldset>
      <div class="table-scroll" tabindex="0" role="region" aria-label="Prévia das matrículas">
      <table>
        <caption>Nota mínima: 6,00 · frequência mínima: 75%</caption>
        <thead><tr><th scope="col">Matrícula</th><th scope="col">Estudante</th><th scope="col">Nota</th><th scope="col">Frequência</th><th scope="col">Resultado ou pendência</th></tr></thead>
        <tbody>
          <asp:Repeater ID="PreviewRows" runat="server"><ItemTemplate>
          <tr data-pending="<%#: Eval("Pending") %>"><td><%#: Eval("Registration") %></td><td><%#: Eval("Name") %></td><td><%#: Eval("Grade") %></td><td><%#: Eval("Attendance") %></td><td><%#: Eval("Result") %></td></tr>
          </ItemTemplate></asp:Repeater>
        </tbody>
      </table>
      </div>
      <asp:Panel ID="CommitPanel" Visible="false" runat="server">
        <fieldset><legend>Enviar para conferência</legend>
          <asp:HiddenField ID="Fingerprint" runat="server" />
          <div class="fields">
            <div><asp:Label AssociatedControlID="RegistrarKey" runat="server">Chave da secretaria</asp:Label><asp:TextBox ID="RegistrarKey" TextMode="Password" MaxLength="128" autocomplete="off" runat="server" /></div>
            <asp:Button ID="ImportButton" Text="Confirmar importação" OnClick="ImportClick" CssClass="button" runat="server" />
          </div>
        </fieldset>
      </asp:Panel>
    </asp:Panel>
  </form>
</main>
</body>
</html>
