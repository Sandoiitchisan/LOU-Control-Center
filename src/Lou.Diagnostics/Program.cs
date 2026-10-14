using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Lou.Diagnostics;

public static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--collect")
        {
            // Isolated, unprivileged worker. A stuck driver cannot trap the GUI indefinitely.
            using var deadline = new Timer(_ => Environment.Exit(2), null, 30000, Timeout.Infinite);
            try
            {
                WindowsInventory.Collect(r => { File.WriteAllText(args[1] + ".tmp", r.Text(), new UTF8Encoding(false)); File.Move(args[1] + ".tmp", args[1], true); });
                return 0;
            }
            catch { return 1; } // Never serialize an exception that could contain paths or personal data.
        }
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show("LOU Diagnostics could not complete this operation. / Impossible de terminer cette opération."); e.Handled = true; };
        return app.Run(new DiagnosticWindow(args));
    }
}

public sealed class DiagnosticWindow : Window
{
    private bool french = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";
    private readonly List<Action> translate = [];
    private readonly TextBox report = new() { IsReadOnly = true, AcceptsReturn = true, FontFamily = new("Consolas"), FontSize = 12,
        TextWrapping = TextWrapping.Wrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Background = Brush("#0B1119"), Foreground = Brush("#D8E4ED"), BorderThickness = new(0), Padding = new(16) };
    private readonly TextBlock status = new() { Foreground = Brush("#72E4D0"), Margin = new(0, 14, 0, 12), TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Height = 3, IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly Button save = new(), issue = new(), collect = new(), folder = new();
    private Process? worker;
    private string? savedPath;
    private bool closing, running;
    private readonly bool review;
    private readonly string? reviewPath;
    private string T(string fr, string en) => french ? fr : en;
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    public DiagnosticWindow(string[] args)
    {
        review = args.Length == 2 && args[0] == "--review-ui"; reviewPath = review ? args[1] : null;
        Title = "LOU Diagnostics"; Width = 1040; Height = 800; MinWidth = 780; MinHeight = 600;
        Background = Brush("#101721"); Foreground = Brush("#EDF4F9"); FontFamily = new("Segoe UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { Margin = new(28) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var head = new StackPanel();
        var title = new DockPanel();
        var language = new ComboBox { ItemsSource = new[] { "Français", "English" }, SelectedIndex = french ? 0 : 1, Width = 100, Height = 30, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(language, Dock.Right); title.Children.Add(language);
        title.Children.Add(new TextBlock { Text = "LOU Diagnostics", FontSize = 30, FontWeight = FontWeights.SemiBold }); head.Children.Add(title);
        head.Children.Add(Label("Comprendre votre écran. Faire avancer LOU.", "Understand your screen. Help improve LOU.", 17));
        head.Children.Add(Label("Écrans internes et USB • Aucun moniteur classique • Aucun diagnostic RGB", "Internal and USB screens • No desktop monitors • No RGB diagnostics"));
        head.Children.Add(Label("Collecte locale, sans connexion réseau. Aucun réglage matériel modifié. Les identifiants personnels sont retirés du rapport.",
            "Local collection, no network connection. Hardware settings remain unchanged. Personal identifiers are removed from the report."));
        root.Children.Add(head);
        var toolbar = new StackPanel(); var buttons = new WrapPanel();
        Configure(collect, "Relancer le diagnostic", "Run diagnostics again", async () => await Collect());
        Configure(save, "Enregistrer le rapport…", "Save report…", () => { Save(); return Task.CompletedTask; });
        Configure(folder, "Afficher le fichier", "Show file", () =>
        {
            if (savedPath != null && File.Exists(savedPath)) { var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; start.ArgumentList.Add("/select,"); start.ArgumentList.Add(savedPath); Process.Start(start); }
            return Task.CompletedTask;
        });
        folder.Visibility = Visibility.Collapsed;
        foreach (var b in new[] { collect, save, folder }) buttons.Children.Add(b);
        toolbar.Children.Add(buttons); toolbar.Children.Add(status); toolbar.Children.Add(progress); Grid.SetRow(toolbar, 1); root.Children.Add(toolbar);
        var frame = new Border { Background = Brush("#0B1119"), BorderBrush = Brush("#2A3B4B"), BorderThickness = new(1), CornerRadius = new(10), Child = report };
        Grid.SetRow(frame, 2); root.Children.Add(frame);
        var footer = new StackPanel { Margin = new(0, 12, 0, 0) };
        footer.Children.Add(Label("Vérifiez le contenu ci-dessus. Sur GitHub, joignez le fichier .txt à votre issue par glisser-déposer ou avec le bouton de pièce jointe.",
            "Review the contents above. On GitHub, attach the .txt file to your issue by dragging it in or using the attachment button."));
        Configure(issue, "Créer une issue sur GitHub ↗", "Create a GitHub issue ↗", () =>
        {
            Process.Start(new ProcessStartInfo(DisplayRules.IssueUrl) { UseShellExecute = true }); return Task.CompletedTask;
        });
        issue.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(issue);
        footer.Children.Add(Label("Ce bouton ouvre votre navigateur. Rien n’est envoyé automatiquement. Un compte GitHub est nécessaire pour publier.",
            "This button opens your browser. Nothing is sent automatically. A GitHub account is required to post."));
        Grid.SetRow(footer, 3); root.Children.Add(footer); Content = root;
        language.SelectionChanged += (_, _) => { french = language.SelectedIndex == 0; foreach (var update in translate) update(); UpdateStatus(); };
        foreach (var update in translate) update();
        Closed += (_, _) => { closing = true; try { if (worker is { HasExited: false }) worker.Kill(true); } catch { } };
        Loaded += async (_, _) =>
        {
            await Collect();
            if (review && reviewPath != null)
            {
                await Task.Delay(300); UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(reviewPath); encoder.Save(stream); Close();
            }
        };
    }
    private TextBlock Label(string fr, string en, double size = 13)
    {
        var text = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 5), Foreground = Brush(size > 14 ? "#72E4D0" : "#AABBCB") };
        translate.Add(() => text.Text = T(fr, en)); return text;
    }
    private void Configure(Button b, string fr, string en, Func<Task> action)
    {
        b.Padding = new(16, 9, 16, 9); b.Margin = new(0, 4, 10, 4); b.Background = Brush("#203B43"); b.Foreground = Brush("#FFFFFF"); b.BorderBrush = Brush("#3C6873");
        translate.Add(() => b.Content = T(fr, en)); b.Click += async (_, _) =>
        {
            try { await action(); }
            catch { status.Text = T("Impossible de terminer cette action. Le rapport reste visible ; vous pouvez réessayer.", "Unable to complete this action. The report remains visible; you can try again."); }
        };
    }
    private void UpdateStatus() => status.Text = running ? T("Analyse en cours… maximum 35 secondes.", "Collecting… up to 35 seconds.") :
        savedPath == null ? T("Rapport visible ci-dessous. Vous pouvez l’enregistrer à l’emplacement de votre choix.", "Your report is shown below. Save it wherever you choose.") :
        T("Rapport enregistré. Vérifiez-le, puis joignez-le à votre issue GitHub.", "Report saved. Review it, then attach it to your GitHub issue.");
    private async Task Collect()
    {
        if (running) return;
        running = true; savedPath = null; folder.Visibility = Visibility.Collapsed; collect.IsEnabled = save.IsEnabled = folder.IsEnabled = issue.IsEnabled = false; progress.Visibility = Visibility.Visible; UpdateStatus();
        string temp = Path.Combine(Path.GetTempPath(), "lou-display-report-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--collect"); start.ArgumentList.Add(temp);
            worker = Process.Start(start) ?? throw new InvalidOperationException();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            try { await worker.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!worker.HasExited) worker.Kill(true); await worker.WaitForExitAsync(); }
            if (closing) return;
            report.Text = File.Exists(temp) && new FileInfo(temp).Length < 2 * 1024 * 1024 ? await File.ReadAllTextAsync(temp) : new DisplayReport().Text();
            if (worker.ExitCode != 0) report.AppendText("\nPARTIAL COLLECTION: a Windows query failed or exceeded its deadline. No hardware setting changed.\n");
        }
        catch { if (!closing) report.Text = new DisplayReport { Notes = ["Collection could not finish. No private exception details exported."] }.Text(); }
        finally
        {
            worker?.Dispose(); worker = null;
            foreach (var p in new[] { temp, temp + ".tmp" }) try { File.Delete(p); } catch { }
            running = false;
            if (!closing) { collect.IsEnabled = save.IsEnabled = issue.IsEnabled = true; progress.Visibility = Visibility.Collapsed; UpdateStatus(); }
        }
        if (!closing && !review) Save();
    }
    private void Save()
    {
        var picker = new SaveFileDialog { Title = T("Enregistrer le rapport LOU Diagnostics", "Save LOU Diagnostics report"), Filter = "Text report (*.txt)|*.txt", DefaultExt = ".txt", FileName = "LOU-Diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt", AddExtension = true, OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) return;
        try { File.WriteAllText(picker.FileName, report.Text, new UTF8Encoding(false)); savedPath = picker.FileName; folder.IsEnabled = true; folder.Visibility = Visibility.Visible; UpdateStatus(); }
        catch { status.Text = T("Enregistrement impossible ici. Choisissez un autre emplacement ; le rapport reste disponible.", "Unable to save here. Choose another location; the report is still available."); }
    }
}
