using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Lou.Core;
using Lou.Providers;

namespace Lou.App;

public partial class MainWindow
{
    private static string AppVersion => typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
    private readonly ReleaseUpdates updates = new();
    private readonly CancellationTokenSource updateStop = new();
    private ReleaseUpdate? updateResult;
    private bool checkingUpdate;

    private static void SetLanguage(AppLanguage language)
    {
        Localizer.Preferred = language;
        CultureInfo.CurrentCulture = Localizer.Culture;
        CultureInfo.CurrentUICulture = Localizer.Culture;
        foreach (var pair in Localizer.UiResources)
            System.Windows.Application.Current.Resources[pair.Key] = Localizer.T(pair.Value);
    }
    private void RefreshLanguage()
    {
        SetLanguage(running.Language);
        rgb?.SetLanguage(running.Language==AppLanguage.English);
        filling = true;
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Localizer.Culture.IetfLanguageTag);
        LanguageChoice.ItemsSource = new[] { new Choice<AppLanguage>(AppLanguage.French, "Français"), new(AppLanguage.English, "English") };
        LanguageChoice.SelectedValue = editing.Language;
        Mode.ItemsSource = Labels.Modes;
        FixedPage.ItemsSource = Labels.Pages;
        PageEffect.ItemsSource = Labels.Transitions;
        var page = BackgroundPage.SelectedValue;
        BackgroundPage.ItemsSource = Labels.Pages;
        BackgroundPage.SelectedValue = page ?? PageId.System;
        var accent = Accent.SelectedValue;
        Accent.ItemsSource = new[] {new Choice<string>("#00E5FF","Cyan"),new("#FF2D95","Magenta"),new("#9B5CFF",Localizer.T("Violet")),new("#68E4CF",Localizer.T("Menthe")),new("#FF9F3C","Orange")};
        Accent.SelectedValue = accent ?? editing.Accent;
        int i=0;
        foreach (var role in Enum.GetValues<FanRole>())
        {
            ((TextBlock)((StackPanel)FanEditors.Children[i++]).Children[0]).Text = CoolingLabels.Label(role);
            var controls = fanControls[role];
            controls.Label.ToolTip = Localizer.T("Nom affiché sur les écrans");
            controls.Connector.ToolTip = Localizer.T("Connecteur de la carte mère (facultatif)");
            controls.Sensor.ToolTip = Localizer.T("Capteur matériel à associer");
        }
        tray.ContextMenuStrip!.Items[0].Text = Localizer.T("Ouvrir");
        tray.ContextMenuStrip.Items[1].Text = Localizer.T("Quitter");
        HeaderStatus.Text = previewOnly ? Localizer.T("APERÇU · AUCUN ENVOI USB") : (ConfigStore.IsInstalled ? "VERSION " : "PORTABLE · ") + AppVersion;
        if (ConfigStore.IsInstalled)
        {
            ActivateSensors.Content = Localizer.T("Vérifier la collecte");
            SensorHelp.Text = Localizer.T("Les capteurs sont lus automatiquement par le composant installé. Aucune modification des courbes de ventilation.");
            StorageHelp.Text = Localizer.T("Réglages et images enregistrés pour votre compte Windows. Le collecteur de capteurs est géré par l’installation.");
        }
        CurrentVersion.Text = Localizer.F($"Version actuelle : {AppVersion}");
        BackgroundInfo.Text = "";
        UpdateReleaseText();
        filling = false;
        FillGlobal();
        FillSensors(false);
        RefreshDisplayChoices();
    }
    private async void CheckUpdateClick(object sender, RoutedEventArgs e) => await CheckUpdates(false);
    private async Task CheckUpdates(bool automatic)
    {
        if (checkingUpdate || disposed) return;
        checkingUpdate = true;
        CheckUpdateButton.IsEnabled = false;
        UpdateStatus.Text = Localizer.T("Vérification…");
        try
        {
            updateResult = await updates.CheckAsync(AppVersion, ConfigStore.IsInstalled, updateStop.Token);
            if (disposed) return;
            UpdateReleaseText();
            if (automatic && updateResult.State == UpdateState.Newer)
                tray.ShowBalloonTip(5000, "LOU Control Center", Localizer.F($"La version {updateResult.Version} est disponible."), System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (OperationCanceledException) { }
        finally { checkingUpdate = false; if (!disposed) CheckUpdateButton.IsEnabled = true; }
    }
    private void UpdateReleaseText()
    {
        UpdateStatus.Text = updateResult?.State switch
        {
            UpdateState.Newer => Localizer.F($"La version {updateResult.Version} est disponible."),
            UpdateState.Current when updateResult.Version == AppVersion => Localizer.F($"Vous utilisez la dernière version ({AppVersion})."),
            UpdateState.Current => Localizer.T("Aucune version plus récente disponible."),
            UpdateState.Unavailable => Localizer.T("Vérification impossible. Réessayez plus tard."),
            _ => ""
        };
        bool available = updateResult?.State == UpdateState.Newer;
        DownloadUpdateButton.Visibility = UpdateBanner.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        DownloadUpdateButton.Content = Localizer.T(updateResult?.Download != null ? "Télécharger la mise à jour" : "Consulter la version sur GitHub");
        UpdateBanner.Content = UpdateStatus.Text;
    }
    private void DownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        if (updateResult is not { State: UpdateState.Newer, Page: not null }) return;
        try
        {
            Process.Start(new ProcessStartInfo((updateResult.Download ?? updateResult.Page).AbsoluteUri) { UseShellExecute = true });
            UpdateStatus.Text = Localizer.T(ConfigStore.IsInstalled
                ? "Téléchargement ouvert dans votre navigateur. Fermez LOU avant d’installer la mise à jour."
                : "Téléchargement ouvert. Extrayez le ZIP dans un nouveau dossier et transférez votre dossier Data après avoir quitté LOU.");
        }
        catch (Exception ex) { UpdateStatus.Text = ex.Message; }
    }
    private void GoUpdates(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 5;
}
