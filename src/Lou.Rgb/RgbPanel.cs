using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace Lou.Rgb;

/// <summary>Shared RGB surface for the universal and personal editions. It owns no LCD or cooling control.</summary>
public sealed class RgbPanel : UserControl, IDisposable
{
    private readonly string path;
    private readonly bool preview;
    private readonly OpenRgbEngine engine = new();
    private readonly CancellationTokenSource stop = new();
    private OpenRgbClient client = new();
    private readonly RgbAnimations animations = new();
    private RgbSettings settings = new();
    private IReadOnlyList<RgbDevice> inventory = [];
    private bool english, busy, filling, disposed, settingsValid = true;
    private readonly List<Action> languageUpdates = [];
    private readonly List<Button> actionButtons = [];
    private readonly Dictionary<RgbLightingGroup, Button> groupButtons = [];
    private RgbLightingGroup group = RgbLightingGroup.Case;
    private readonly CheckBox individual = new();
    private readonly TextBlock groupSummary = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 10) };
    private sealed record DeviceChoice(RgbDevice Device, string Label) { public override string ToString() => Label; }
    private sealed record EffectChoice(RgbMode Mode, string Effect, string Label) { public override string ToString() => Label; }
    private RgbDevice? SelectedDevice => (devices.SelectedItem as DeviceChoice)?.Device;
    private bool WholeCase => SelectedDevice?.Type == 0 && zones.SelectedIndex == 0;
    private readonly ComboBox devices = new(), zones = new(), modes = new(), profiles = new();
    private readonly ComboBox frameRate = new() { ItemsSource = new[] { 30, 60 }, SelectedIndex = 0 };
    private readonly TextBox color = new() { Text = "#00E5FF", MaxLength = 7 }, name = new() { MaxLength = 60 }, ledCount = new(), caseLimit = new() { Text = "0", MaxLength = 3 };
    private readonly Slider brightness = new() { Minimum = 0, Maximum = 100, Value = 100 }, speed = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly CheckBox startup = new(), applyStartup = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12), Foreground = new SolidColorBrush(Color.FromRgb(104, 228, 207)) };
    private readonly TextBlock identity = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = .65 };
    private readonly StackPanel editor = new();
    private readonly Border swatch = new() { Width = 36, Height = 28, CornerRadius = new(6), Margin = new(8) };
    private string T(string fr, string en) => english ? en : fr;
    public RgbPanel(string settingsPath, bool previewOnly, bool useEnglish)
    {
        path = settingsPath; preview = previewOnly; english = useEnglish;
        animations.Failed += e => Dispatcher.BeginInvoke(new Action(() => { if (!disposed) SetStatus(Error(e, false), Error(e, true)); }));
        try { settings = RgbSettings.Load(path); }
        catch { settingsValid = false; }
        var root = new StackPanel();
        root.Children.Add(Label("Éclairage RGB", "RGB lighting", 30));
        root.Children.Add(Label("Choisissez un éclairage, un motif, et appliquez.", "Choose your lights, pick an effect, and apply.", 14));
        var connection = new StackPanel();
        var connectionAdvanced = new StackPanel();
        var buttons = new WrapPanel();
        buttons.Children.Add(ActionButton("Activer le RGB", "Enable RGB", () => Connect(false)));
        buttons.Children.Add(ActionButton("Détecter aussi la RAM…", "Detect RAM too…", () => Connect(true)));
        connectionAdvanced.Children.Add(ActionButton("Serveur OpenRGB existant", "Existing OpenRGB server", async () => { await animations.StopAsync(); client = new(6742); await Refresh(); }));
        connectionAdvanced.Children.Add(ActionButton("Actualiser", "Refresh", Refresh));
        connectionAdvanced.Children.Add(ActionButton("Déconnecter", "Disconnect", async () => { await animations.StopAsync(); engine.Stop(); inventory = []; FillInventory(); }));
        connection.Children.Add(buttons);
        connectionAdvanced.Children.Add(Label("L’accès complet demande l’autorisation Windows pour la RAM et certaines cartes mères. Un serveur existant doit utiliser le port local 6742 et OpenRGB 1.0 ou ultérieur.",
            "Full access requests Windows permission for RAM and some motherboards. An existing server must use localhost port 6742 and OpenRGB 1.0 or later."));
        connection.Children.Add(status); root.Children.Add(connection);
        var sections = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new(0, 4, 0, 16) };
        foreach (var scope in new[] { RgbLightingGroup.Case, RgbLightingGroup.Ram, RgbLightingGroup.Gpu, RgbLightingGroup.All })
        {
            var b = new Button { MinHeight = 76, Margin = new(0, 0, 8, 0), Padding = new(12), HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left };
            groupButtons.Add(scope, b); sections.Children.Add(b);
            b.Click += (_, _) => { if (busy) return; group = scope; individual.IsChecked = false; FillInventory(); };
        }
        root.Children.Add(sections); root.Children.Add(groupSummary);
        editor.Children.Add(Label("Motif", "Effect", 18));
        var advanced = new StackPanel();
        languageUpdates.Add(() => individual.Content = T("Choisir un périphérique ou une zone en particulier", "Choose a specific device or zone"));
        advanced.Children.Add(individual); advanced.Children.Add(devices); advanced.Children.Add(identity);
        var zoneDetails = new Expander { Content = zones, Foreground = new SolidColorBrush(Color.FromRgb(210, 226, 235)) };
        var zoneHeader = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(210, 226, 235)) }; zoneDetails.Header = zoneHeader;
        languageUpdates.Add(() => zoneHeader.Text = T("Réglage par zone (avancé)", "Per-zone settings (advanced)"));
        advanced.Children.Add(zoneDetails); editor.Children.Add(modes);
        advanced.Children.Add(Label("Une zone sans effets indépendants utilise le mode actif du périphérique. Activez d’abord un mode à couleur fixe sur « Tout le périphérique ».",
            "A zone without independent effects uses the device’s active mode. First select a fixed-color mode for ‘Whole device’."));
        var colors = new WrapPanel(); colors.Children.Add(swatch);
        colors.Children.Add(ActionButton("Choisir la couleur…", "Choose color…", () =>
        {
            using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try { picker.Color = System.Drawing.ColorTranslator.FromHtml(color.Text); } catch { }
            if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) color.Text = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
            return Task.CompletedTask;
        }));
        foreach (string hex in new[] { "#00E5FF", "#FF2D95", "#9B5CFF", "#FFFFFF", "#000000" })
        {
            var b = new Button { Width = 30, Height = 30, Margin = new(4), Background = (Brush)new BrushConverter().ConvertFromString(hex)!, ToolTip = hex };
            b.Click += (_, _) => color.Text = hex; colors.Children.Add(b);
        }
        editor.Children.Add(colors);
        var adjustments = new Grid(); adjustments.ColumnDefinitions.Add(new()); adjustments.ColumnDefinitions.Add(new());
        var light = new StackPanel { Margin = new(0, 0, 24, 0) }; light.Children.Add(Label("Luminosité", "Brightness")); light.Children.Add(brightness);
        var motion = new StackPanel(); motion.Children.Add(Label("Vitesse", "Speed")); motion.Children.Add(speed);
        Grid.SetColumn(motion, 1); adjustments.Children.Add(light); adjustments.Children.Add(motion); editor.Children.Add(adjustments);
        advanced.Children.Add(Label("Couleur personnalisée", "Custom color")); advanced.Children.Add(color);
        advanced.Children.Add(Label("Fluidité de l’animation (images/s)", "Animation smoothness (frames/s)")); advanced.Children.Add(frameRate);
        advanced.Children.Add(Label("30 : équilibré · 60 : fluidité maximale, selon le contrôleur. La vitesse de l’effet reste identique.",
            "30: balanced · 60: maximum smoothness, depending on the controller. Effect speed stays the same."));
        var applyButtons = new WrapPanel();
        applyButtons.Children.Add(ActionButton("Appliquer", "Apply", async () =>
        {
            var plan = Selections(); await ApplySelections(plan);
            foreach (var selection in plan) RememberTuning(selection); if (settingsValid) Persist();
            SetStatus("Éclairage appliqué.", "Lighting applied.");
        }));
        applyButtons.Children.Add(ActionButton("Arrêter les animations", "Stop animations", async () =>
        { await animations.StopEffectsAsync(client, stop.Token); SetStatus("Animations arrêtées.", "Animations stopped."); }));
        editor.Children.Add(applyButtons);
        advanced.Children.Add(Label("Plage ARGB du groupe boîtier (0 = capacité complète)", "Case group ARGB range (0 = full capacity)")); advanced.Children.Add(caseLimit);
        advanced.Children.Add(Label("Sur les contrôleurs Gigabyte compatibles, une plage adaptée au câblage allège les animations. La valeur couvre chaque sortie : vérifiez qu’aucune LED ne reste figée. Elle est enregistrée avec le profil.",
            "On compatible Gigabyte controllers, matching the range to your wiring reduces animation traffic. The value covers each output: check that no LED stays frozen. Saved with the profile."));
        advanced.Children.Add(Label("Nombre de LED de la zone ARGB", "ARGB zone LED count")); advanced.Children.Add(ledCount);
        advanced.Children.Add(ActionButton("Définir le nombre de LED", "Set LED count", async () =>
        {
            if (!int.TryParse(ledCount.Text, out int count)) throw new InvalidDataException("RGB_ZONE_SIZE_INVALID");
            var selected = IdentitySelection();
            if (zones.SelectedItem is not RgbZone zone) throw new InvalidDataException("RGB_ZONE_SIZE_INVALID");
            await animations.StopAsync(selected.Identity);
            await client.ResizeAsync(selected, count, stop.Token);
            settings.ZoneSizes.RemoveAll(z => z.Device == selected.Identity && z.Connection == zone.ConnectionIdentity);
            settings.ZoneSizes.Add(new() { Device = selected.Identity, Connection = zone.ConnectionIdentity, Count = count });
            Persist(); await Refresh();
        }));
        advanced.Children.Add(Label("Saisissez le nombre réel de LED branchées. Un hub qui duplique le signal apparaît comme une seule zone. Les anciens profils de cette zone devront être enregistrés à nouveau.",
            "Enter the actual connected LED count. A hub that duplicates the signal appears as one zone. Existing profiles for this zone must be saved again."));
        var expander = new Expander { Content = advanced, Foreground = new SolidColorBrush(Color.FromRgb(210, 226, 235)) };
        var advancedHeader = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(210, 226, 235)) }; expander.Header = advancedHeader;
        advanced.Children.Add(connectionAdvanced);
        languageUpdates.Add(() => advancedHeader.Text = T("Réglages avancés", "Advanced settings"));
        root.Children.Add(Card(editor)); root.Children.Add(expander);
        var saved = new StackPanel(); saved.Children.Add(Label("Profils", "Profiles", 18));
        saved.Children.Add(Label("Enregistrez vos sélections sous le même nom pour créer une ambiance regroupant plusieurs périphériques.",
            "Save selections under the same name to build a scene covering several devices."));
        saved.Children.Add(profiles); saved.Children.Add(name);
        var profileButtons = new WrapPanel();
        profileButtons.Children.Add(ActionButton("Enregistrer", "Save", SaveSelection));
        profileButtons.Children.Add(ActionButton("Appliquer le profil", "Apply profile", ApplyProfile));
        profileButtons.Children.Add(ActionButton("Supprimer le profil", "Delete profile", () =>
        {
            if (profiles.SelectedItem is RgbProfile p) settings.Profiles.Remove(p);
            Persist(); FillProfiles(); return Task.CompletedTask;
        }));
        saved.Children.Add(profileButtons);
        languageUpdates.Add(() => startup.Content = T("Activer le moteur RGB au lancement de LOU", "Start the RGB engine with LOU"));
        languageUpdates.Add(() => applyStartup.Content = T("Appliquer ce profil au lancement", "Apply this profile on startup"));
        startup.IsChecked = settings.StartWithApp; applyStartup.IsChecked = settings.ApplyOnStart;
        saved.Children.Add(startup); saved.Children.Add(applyStartup);
        saved.Children.Add(Label("Le démarrage automatique utilise les droits ordinaires. Pour les périphériques nécessitant des droits administrateur, activez l’accès complet. Les options RGB sont enregistrées ici, indépendamment des réglages d’écran.",
            "Automatic startup uses standard permissions. For devices requiring administrator rights, enable full access. RGB options are saved here, independently of display settings."));
        var scenes = new Expander { Content = Card(saved), Margin = new(0, 10, 0, 0) };
        var sceneHeader = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(210, 226, 235)) }; scenes.Header = sceneHeader;
        languageUpdates.Add(() => sceneHeader.Text = T("Mes ambiances et démarrage", "My scenes and startup")); root.Children.Add(scenes);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        devices.SelectionChanged += (_, _) => FillZones(); zones.SelectionChanged += (_, _) => FillModes();
        individual.Click += (_, _) => FillInventory();
        modes.SelectionChanged += (_, _) => FillMode();
        profiles.SelectionChanged += (_, _) => { if (profiles.SelectedItem is RgbProfile p) { name.Text = p.Name; LoadProfileSelection(); } };
        color.TextChanged += (_, _) => { try { swatch.Background = (Brush)new BrushConverter().ConvertFromString(color.Text)!; } catch { swatch.Background = Brushes.Transparent; } };
        startup.Click += (_, _) => SaveStartup(); applyStartup.Click += (_, _) => SaveStartup();
        if (preview) inventory = PreviewDevices();
        FillProfiles(); FillInventory(); SetLanguage(english);
        if (!settingsValid) SetStatus("Le fichier RGB est illisible et reste intact. Restaurez rgb.json.bak avant de modifier les profils.", "The RGB file is unreadable and has been preserved. Restore rgb.json.bak before editing profiles.");
        else SetStatus(preview ? "Aperçu : aucun accès au matériel RGB." : "RGB désactivé. Vos LED conservent leur état actuel.",
            preview ? "Preview: no RGB hardware access." : "RGB disabled. Your LEDs keep their current state.");
        foreach (var button in actionButtons) button.IsEnabled = !preview;
        startup.IsEnabled = applyStartup.IsEnabled = !preview && settingsValid;
        // RGB has its own settings store; parent-window form handlers must not mark LCD settings dirty.
        AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, e) => e.Handled = true));
        AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, e) => e.Handled = true));
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler((_, e) => e.Handled = true));
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent, new RoutedEventHandler((_, e) => e.Handled = true));
    }
    public void SetLanguage(bool useEnglish)
    {
        english = useEnglish; foreach (var update in languageUpdates) update();
        FillInventory();
    }
    private TextBlock Label(string fr, string en, double size = 12)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, Margin = new(0, 7, 0, 10), Opacity = size < 14 ? .75 : 1 };
        languageUpdates.Add(() => text.Text = T(fr, en)); return text;
    }
    private static Border Card(UIElement content) => new() { Child = content, Background = new SolidColorBrush(Color.FromRgb(16, 23, 31)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(37, 48, 58)), BorderThickness = new(1), CornerRadius = new(12), Padding = new(22), Margin = new(0, 10, 0, 10) };
    private Button ActionButton(string fr, string en, Func<Task> action)
    {
        var button = new Button { Margin = new(0, 8, 10, 8), Padding = new(14, 8, 14, 8) };
        languageUpdates.Add(() => button.Content = T(fr, en)); actionButtons.Add(button);
        button.Click += async (_, _) => await Run(action); return button;
    }
    private async Task Run(Func<Task> action)
    {
        if (busy || disposed || preview) return;
        busy = true; foreach (var b in actionButtons) b.IsEnabled = false;
        try { await action(); }
        catch (OperationCanceledException) { if (!disposed) SetStatus("Connexion RGB interrompue ou expirée.", "RGB connection cancelled or timed out."); }
        catch (Exception e) { SetStatus(Error(e, false), Error(e, true)); }
        finally { busy = false; if (!disposed) foreach (var b in actionButtons) b.IsEnabled = true; }
    }
    private void SetStatus(string fr, string en) => status.Text = T(fr, en);
    public async Task StartAsync()
    {
        if (!preview && settingsValid && settings.StartWithApp)
            await Run(async () => { await Connect(false); if (settings.ApplyOnStart) await ApplyProfileNamed(settings.StartupProfile); });
    }
    private async Task Connect(bool elevated)
    {
        await animations.StopAsync();
        if (elevated && engine.IsOwned) { engine.Stop(); await Task.Delay(1200, stop.Token); }
        SetStatus("Détection des périphériques RGB…", "Detecting RGB devices…");
        await Task.Run(() => engine.StartAsync(elevated, stop.Token), stop.Token);
        client = new(); await RestoreZoneSizes(); await Refresh();
    }
    private async Task RestoreZoneSizes()
    {
        if (!settingsValid) return;
        foreach (var saved in settings.ZoneSizes)
        {
            var current = await client.DiscoverAsync(stop.Token);
            var matches = current.Where(d => d.HasPhysicalIdentity && d.Identity == saved.Device).ToArray();
            if (matches.Length != 1) continue;
            var d = matches[0]; var candidates = d.Zones.Where(z => z.ConnectionIdentity == saved.Connection).ToArray();
            if (candidates.Length != 1 || candidates[0].Count == saved.Count) continue;
            await client.ResizeAsync(new() { Identity = d.Identity, Topology = d.Topology, Zone = candidates[0].Signature }, saved.Count, stop.Token);
        }
    }
    private async Task Refresh()
    {
        inventory = await client.DiscoverAsync(stop.Token); FillInventory();
        SetStatus($"{inventory.Count} périphérique(s) détecté(s). Aucune couleur modifiée par cette recherche.",
            $"{inventory.Count} device(s) detected. This refresh did not change colors.");
    }
    private void FillInventory()
    {
        string? old = SelectedDevice?.Identity;
        var targets = RgbLightingGroups.Targets(inventory, group);
        foreach (var (scope, button) in groupButtons)
        {
            string title = scope switch { RgbLightingGroup.Case => T("Boîtier + ventilateurs", "Case + fans"), RgbLightingGroup.Ram => "RAM", RgbLightingGroup.Gpu => "GPU", _ => T("Tout synchroniser", "Sync all") };
            string subtitle = scope == RgbLightingGroup.Gpu ? T("Bientôt", "Coming later") : inventory.Count == 0 ? T("À activer", "Enable RGB first") :
                RgbLightingGroups.Targets(inventory, scope).Length == 0 ? T("Non détecté", "Not detected") : scope == RgbLightingGroup.All ? T("Un seul motif partout", "One effect everywhere") : T("Disponible", "Available");
            var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Opacity = .7, Margin = new(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap }); button.Content = content;
            button.Background = new SolidColorBrush(scope == group ? Color.FromRgb(27, 61, 59) : Color.FromRgb(17, 26, 34));
            button.BorderBrush = new SolidColorBrush(scope == group ? Color.FromRgb(104, 228, 207) : Color.FromRgb(37, 48, 58)); button.BorderThickness = new(1);
        }
        groupSummary.Text = group == RgbLightingGroup.Gpu ? T("Le contrôle de l’éclairage GPU sera ajouté ultérieurement.", "GPU lighting control will be added later.") :
            targets.Length == 0 ? (inventory.Count == 0 ? T("Activez le RGB pour retrouver vos éclairages.", "Enable RGB to find your lights.") :
                T("Aucun éclairage détecté dans cette section. Pour la RAM, essayez « Détecter aussi la RAM… ».", "No lights detected in this section. For RAM, try ‘Detect RAM too…’.")) :
            group == RgbLightingGroup.All ? T("Même motif, couleur et vitesse pour le boîtier et la RAM. Le défilement peut varier selon le matériel.", "Same effect, color and speed for case and RAM. Timing may vary between devices.") :
            group == RgbLightingGroup.Case ? T("Tous les éclairages du boîtier et des ventilateurs, ensemble.", "All case and fan lighting together.") : T("Toutes vos barrettes, ensemble.", "All your RAM modules together.");
        var visible = individual.IsChecked == true ? inventory : targets;
        var choices = visible.OrderBy(d => d.Type is 0 or 1 or 2 ? d.Type : 99).Select(d => new DeviceChoice(d,
            (d.Type switch { 0 => T("Boîtier et ventilateurs", "Case and fans"), 1 => "RAM", 2 => T("Carte graphique", "Graphics card"), _ => T("Autre éclairage", "Other lighting") }) + " · " + d.Name)).ToArray();
        devices.ItemsSource = choices; devices.SelectedItem = choices.FirstOrDefault(d => d.Device.Identity == old) ?? choices.FirstOrDefault();
        devices.IsEnabled = zones.IsEnabled = individual.IsChecked == true;
        editor.Visibility = (group != RgbLightingGroup.Gpu && choices.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
        if (choices.Length == 0) { zones.Items.Clear(); modes.ItemsSource = null; identity.Text = ""; }
    }
    private void FillZones()
    {
        filling = true; zones.Items.Clear(); zones.Items.Add(T("Tout le périphérique", "Whole device"));
        if (SelectedDevice is RgbDevice d)
        {
            foreach (var z in d.Zones) zones.Items.Add(z);
            identity.Text = $"{d.Description}\n{d.Location} · {d.Serial}";
        }
        zones.SelectedIndex = 0; filling = false; FillModes();
    }
    private void FillModes()
    {
        if (filling || SelectedDevice is not RgbDevice d) return;
        var zone = zones.SelectedItem as RgbZone;
        var choices = WholeCase ? RgbCaseLighting.Modes(d) : zone?.Modes.Length > 0 ? zone.Modes : d.Modes;
        int active = zone?.Modes.Length > 0 ? zone.ActiveMode : d.ActiveMode;
        if (zone != null && zone.Modes.Length == 0) choices = choices.Where((m, i) => i == active && m.PerLed && (m.Flags & 1024) == 0).ToArray();
        // Prefer the tested uniform direct path for whole-case control. Native effects remain
        // exposed where supported, but cached effect state does not prove hardware compatibility.
        var direct = choices.FirstOrDefault(m => m.Name == "Direct" && RgbEffects.IsDirect(m)) ?? choices.FirstOrDefault(RgbEffects.IsDirect);
        var effects = new List<EffectChoice>();
        if (direct != null) effects.AddRange(RgbEffects.All.Select(e => new EffectChoice(direct, e.Id, english ? e.English : e.French)));
        if (individual.IsChecked == true) effects.AddRange(choices.Select(m => new EffectChoice(m, "", T("Matériel · ", "Hardware · ") + m.Name)));
        modes.ItemsSource = effects; modes.SelectedItem = effects.FirstOrDefault();
        ledCount.IsEnabled = zone != null && (zone.Flags & 3) != 0; ledCount.Text = zone?.Count.ToString() ?? "";
        caseLimit.IsEnabled = WholeCase && direct != null && d.Zones.Any(z => RgbCaseLighting.CanCoverNativeHeader(d, direct, z));
        var tuning = settings.DeviceTuning.FirstOrDefault(t => t.Identity == d.Identity);
        caseLimit.Text = (tuning?.CaseLedLimit ?? 0).ToString(); frameRate.SelectedItem = tuning?.FramesPerSecond == 60 ? 60 : 30;
        LoadProfileSelection();
    }
    private void LoadProfileSelection()
    {
        if (profiles.SelectedItem is not RgbProfile profile || SelectedDevice is not RgbDevice device) return;
        string zone = (zones.SelectedItem as RgbZone)?.Signature ?? "";
        var entry = profile.Devices.FirstOrDefault(d => d.Identity == device.Identity && d.Zone == zone);
        if (entry == null) return;
        var choice = modes.Items.OfType<EffectChoice>().FirstOrDefault(c => c.Mode.Signature == entry.Mode && c.Effect == entry.Effect);
        if (choice == null) return;
        modes.SelectedItem = choice; color.Text = entry.Color; brightness.Value = entry.Brightness;
        speed.Value = entry.Speed; caseLimit.Text = entry.CaseLedLimit.ToString(); frameRate.SelectedItem = entry.FramesPerSecond == 60 ? 60 : 30;
    }
    private void FillMode()
    {
        if (modes.SelectedItem is not EffectChoice choice) { color.IsEnabled = brightness.IsEnabled = speed.IsEnabled = frameRate.IsEnabled = false; return; }
        var m = choice.Mode;
        frameRate.IsEnabled = choice.Effect.Length > 0 && choice.Effect is not ("solid" or "off");
        if (choice.Effect == "rainbow" && zones.SelectedIndex == 0 && SelectedDevice is RgbDevice d && RgbNativeEffects.Rainbow(d) != null) frameRate.IsEnabled = false;
        if (choice.Effect.Length > 0)
        { color.IsEnabled = RgbEffects.All.Single(e => e.Id == choice.Effect).UsesColor; brightness.IsEnabled = choice.Effect != "off"; speed.IsEnabled = choice.Effect is not ("solid" or "off"); return; }
        color.IsEnabled = m.HasColors; brightness.IsEnabled = m.HasBrightness || m.PerLed; speed.IsEnabled = m.HasSpeed;
        if (m.HasBrightness) brightness.Value = ToPercent(m.Brightness, m.BrightnessMin, m.BrightnessMax);
        if (m.HasSpeed) speed.Value = ToPercent(m.Speed, m.SpeedMin, m.SpeedMax);
    }
    private static double ToPercent(uint value, uint min, uint max) => min == max ? 100 : Math.Clamp(100.0 * ((double)value - min) / ((double)max - min), 0, 100);
    private RgbAssignment IdentitySelection()
    {
        if (SelectedDevice is not RgbDevice d || !d.HasPhysicalIdentity || inventory.Count(x => x.Identity == d.Identity) != 1)
            throw new InvalidDataException("RGB_IDENTITY_CHANGED");
        return new() { Identity = d.Identity, Topology = WholeCase ? RgbCaseLighting.Fingerprint(d) : d.Topology, CaseLighting = WholeCase, Label = d.Name, Zone = (zones.SelectedItem as RgbZone)?.Signature ?? "" };
    }
    private RgbAssignment Selection()
    {
        var result = IdentitySelection();
        if (modes.SelectedItem is not EffectChoice choice) throw new InvalidDataException("RGB_DEVICE_MODE_REQUIRED");
        RgbSettings.ParseColor(color.Text); result.Mode = choice.Mode.Signature; result.Effect = choice.Effect; result.Color = color.Text.ToUpperInvariant();
        result.FramesPerSecond = frameRate.SelectedItem is int fps ? fps : 30;
        if (caseLimit.IsEnabled) { if (!int.TryParse(caseLimit.Text, out int limit) || limit is < 0 or > 256) throw new InvalidDataException("RGB_ZONE_SIZE_INVALID"); result.CaseLedLimit = limit; }
        result.Speed = (int)Math.Round(speed.Value); result.Brightness = (int)Math.Round(brightness.Value); return result;
    }
    private RgbAssignment[] Selections()
    {
        if (individual.IsChecked == true) return [Selection()];
        if (modes.SelectedItem is not EffectChoice choice) throw new InvalidDataException("RGB_GROUP_UNSUPPORTED");
        // Technical tuning is preserved independently for every controller when applying a shared look.
        return RgbLightingGroups.Plan(inventory, group, choice.Effect, color.Text.ToUpperInvariant(),
            (int)Math.Round(speed.Value), (int)Math.Round(brightness.Value), settings.DeviceTuning);
    }
    private async Task ApplySelections(RgbAssignment[] plan)
    {
        var current = await client.DiscoverAsync(stop.Token);
        foreach (var entry in plan)
            if (entry.CaseLighting) RgbCaseLighting.Validate(RgbCaseLighting.Resolve(current, entry), entry);
            else RgbIdentity.Resolve(current, entry);
        int applied = 0;
        try { foreach (var entry in plan) { await animations.ApplyAsync(client, entry, stop.Token); applied++; } }
        catch (Exception e) { throw new IOException(T($"Application interrompue : {applied}/{plan.Length} éclairages appliqués. ", $"Application stopped: {applied}/{plan.Length} lights applied. ") + Error(e, english), e); }
    }
    private Task SaveSelection()
    {
        string profileName = name.Text.Trim(); if (profileName.Length == 0) throw new InvalidDataException("RGB_PROFILE_NAME");
        var selections = Selections(); var p = settings.Profiles.FirstOrDefault(p => p.Name == profileName);
        if (p == null) { p = new() { Name = profileName }; settings.Profiles.Add(p); }
        foreach (var selection in selections) {
            p.Devices.RemoveAll(d => d.Identity == selection.Identity && (selection.Zone.Length == 0 || d.Zone.Length == 0 || d.Zone == selection.Zone)); p.Devices.Add(selection);
            RememberTuning(selection);
        }
        Persist(); FillProfiles(profileName); SetStatus("Sélection enregistrée dans le profil.", "Selection saved to profile."); return Task.CompletedTask;
    }
    private void FillProfiles(string? selected = null)
    {
        profiles.ItemsSource = settings.Profiles.ToArray(); profiles.SelectedItem = settings.Profiles.FirstOrDefault(p => p.Name == (selected ?? settings.StartupProfile)) ?? settings.Profiles.FirstOrDefault();
    }
    private Task ApplyProfile() => ApplyProfileNamed((profiles.SelectedItem as RgbProfile)?.Name ?? "");
    private async Task ApplyProfileNamed(string profileName)
    {
        var p = settings.Profiles.FirstOrDefault(p => p.Name == profileName) ?? throw new InvalidDataException("RGB_PROFILE_NAME");
        inventory = await client.DiscoverAsync(stop.Token);
        foreach (var entry in p.Devices) { if (entry.CaseLighting) RgbCaseLighting.Validate(RgbCaseLighting.Resolve(inventory, entry), entry); else RgbIdentity.Resolve(inventory, entry); }
        int applied = 0;
        try { foreach (var entry in p.Devices.OrderBy(d => d.Zone.Length)) { await animations.ApplyAsync(client, entry, stop.Token); applied++; } }
        catch { SetStatus($"Profil interrompu après {applied}/{p.Devices.Count} commandes.", $"Profile interrupted after {applied}/{p.Devices.Count} commands."); throw; }
        SetStatus($"Profil « {p.Name} » envoyé ({applied} sélections).", $"Profile ‘{p.Name}’ sent ({applied} selections).");
    }
    private void SaveStartup()
    {
        if (preview || disposed) return;
        settings.StartWithApp = startup.IsChecked == true; settings.ApplyOnStart = applyStartup.IsChecked == true;
        settings.StartupProfile = (profiles.SelectedItem as RgbProfile)?.Name ?? "";
        try { Persist(); SetStatus("Options de démarrage RGB enregistrées.", "RGB startup options saved."); }
        catch (Exception e) { SetStatus(Error(e, false), Error(e, true)); }
    }
    private void Persist() { if (!settingsValid) throw new InvalidDataException("RGB_SETTINGS_INVALID"); settings.Save(path); }
    private void RememberTuning(RgbAssignment entry)
    {
        if (!settingsValid) return;
        var previous = settings.DeviceTuning.FirstOrDefault(t => t.Identity == entry.Identity);
        settings.DeviceTuning.RemoveAll(t => t.Identity == entry.Identity);
        settings.DeviceTuning.Add(new() { Identity = entry.Identity, FramesPerSecond = entry.FramesPerSecond,
            CaseLedLimit = entry.CaseLighting ? entry.CaseLedLimit : previous?.CaseLedLimit ?? 0 });
    }
    private static string Error(Exception e, bool en) => e.Message switch
    {
        "RGB_GROUP_EMPTY" => en ? "No lights are available in this section." : "Aucun éclairage disponible dans cette section.",
        "RGB_GROUP_UNSUPPORTED" => en ? "An item in this group does not support these effects. Use advanced settings to select it separately." : "Un élément de ce groupe ne prend pas en charge ces motifs. Sélectionnez-le séparément dans les réglages avancés.",
        "RGB_CONFLICT" => en ? "Close other RGB utilities before starting LOU RGB." : "Fermez les autres utilitaires RGB avant d’activer LOU RGB.",
        "RGB_IDENTITY_CHANGED" => en ? "Device absent, ambiguous or changed. Refresh and save its assignment again." : "Périphérique absent, ambigu ou modifié. Actualisez puis enregistrez son association à nouveau.",
        "RGB_DEVICE_MODE_REQUIRED" => en ? "Select a compatible device-wide mode first; this zone has no independent effects." : "Choisissez d’abord un mode compatible sur tout le périphérique ; cette zone n’a pas d’effet indépendant.",
        "RGB_COLOR_INVALID" => en ? "Enter a color such as #00E5FF." : "Saisissez une couleur comme #00E5FF.",
        "RGB_ZONE_EMPTY" => en ? "Set the connected ARGB LED count first." : "Définissez d’abord le nombre de LED ARGB branchées.",
        "RGB_ZONE_SIZE_INVALID" => en ? "LED count outside this zone’s supported range, or fixed-size zone." : "Nombre de LED hors des limites de cette zone, ou zone de taille fixe.",
        "RGB_VERSION_OLD" => en ? "OpenRGB 1.0 or later is required (SDK 6)." : "OpenRGB 1.0 ou ultérieur est nécessaire (SDK 6).",
        "RGB_BUNDLE_INVALID" => en ? "The bundled OpenRGB files are missing or modified. Repair LOU." : "Les fichiers OpenRGB intégrés sont absents ou modifiés. Réparez LOU.",
        "RGB_PROFILE_NAME" => en ? "Choose a profile or enter its name." : "Choisissez un profil ou saisissez son nom.",
        "RGB_SETTINGS_INVALID" => en ? "Invalid RGB configuration. Its original file has been preserved." : "Configuration RGB invalide. Le fichier d’origine est conservé.",
        "RGB_PORT_BUSY" => en ? "LOU’s RGB port is already in use. Close the other LOU RGB instance." : "Le port RGB de LOU est déjà occupé. Fermez l’autre instance RGB de LOU.",
        "RGB_COMMAND_REJECTED:3" => en ? "OpenRGB refused this command. Check access permissions and device capabilities." : "OpenRGB a refusé cette commande. Vérifiez les droits d’accès et les capacités du périphérique.",
        "RGB_COMMAND_REJECTED:4" => en ? "The device disappeared. Refresh detection." : "Le périphérique a disparu. Actualisez la détection.",
        _ => (en ? "RGB unavailable: " : "RGB indisponible : ") + e.Message
    };
    public void Dispose() { if (disposed) return; disposed = true; animations.Dispose(); stop.Cancel(); engine.Dispose(); }
    private static RgbDevice[] PreviewDevices()
    {
        var direct = new RgbMode("Direct", 32, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, []);
        RgbDevice Device(int type, string name) => new((uint)type, type, name, "Preview", "Preview only", "1", "demo-" + type, "Preview only", 0,
            [direct], [new("Preview zone", 1, 12, 12, 12, 0, -1, [])], new uint[12]);
        return [Device(0, "Boîtier / Case"), Device(1, "RAM")];
    }
}
