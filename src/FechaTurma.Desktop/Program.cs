using System;
using System.Windows.Forms;
using FechaTurma.Desktop.Api;

namespace FechaTurma.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var smoke = args.Length == 1 && args[0] == "--smoke";
            Guid target = Guid.Empty;
            var smokeClose = args.Length == 2 && args[0] == "--smoke-close" && Guid.TryParseExact(args[1], "D", out target) && target != Guid.Empty;
            if (args.Length != 0 && !smoke && !smokeClose) return 2;
            var key = Environment.GetEnvironmentVariable("FT_DESKTOP_KEY");
            if ((smoke || smokeClose) && string.IsNullOrWhiteSpace(key)) return 2;

            if (smoke || smokeClose) Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var client = new ClosureClient(ClosureClient.DefaultOrigin))
            using (var form = new CoordinatorForm(client, key))
            {
                if (smoke || smokeClose)
                {
                    // Independent of WinForms message pumping; no modal diagnostics in smoke mode.
                    using (new System.Threading.Timer(_ => Environment.Exit(124), null, 30000, -1))
                    {
                        var result = 1;
                        form.Shown += async (_, __) =>
                        {
                            try { result = await form.RunSmokeAsync(smokeClose ? (Guid?)target : null); }
                            catch (Exception) { result = 1; }
                            finally { form.Close(); }
                        };
                        Application.Run(form);
                        return result;
                    }
                }
                Application.Run(form);
                return 0;
            }
        }
    }
}
