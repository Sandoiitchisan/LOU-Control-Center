using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lou.Core;
using Lou.Devices;
using Lou.Providers;
using Lou.Rendering;
using Microsoft.Win32;
using Button=System.Windows.Controls.Button;
using TextBox=System.Windows.Controls.TextBox;
using ComboBox=System.Windows.Controls.ComboBox;
using MessageBox=System.Windows.MessageBox;
using Color=System.Windows.Media.Color;
using OpenFileDialog=Microsoft.Win32.OpenFileDialog;
using SaveFileDialog=Microsoft.Win32.SaveFileDialog;
namespace Lou.App;

public partial class MainWindow:Window
{
    private Settings editing,running;
    private Lou.Rgb.RgbPanel? rgb;
    private readonly bool previewOnly;
    private readonly DataHub hub;
    private readonly CityProvider cities=new();
    private readonly Dictionary<string,DashboardHost> hosts=[];
    private readonly ConcurrentDictionary<string,BitmapSource> frames=[];
    private readonly Dictionary<FanRole,(TextBox Label,TextBox Connector,ComboBox Sensor,TextBlock Live)> fanControls=[];
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private readonly System.Windows.Forms.NotifyIcon tray;
    private DisplaySettings? selected;
    private bool filling,scanning,closing,disposed;
    private int ticks;
    private CancellationTokenSource searchStop=new();
    private readonly record struct GpuOption(string Id,string Hardware,string DeviceId=""){public override string ToString()=>Id==""?Localizer.T("Automatique (si une seule carte est disponible)"):Hardware+" · "+Id;}
    private sealed record SensorOption(HardwareReading? Reading,string Board="",string Topology="",string? MissingLabel=null)
    {
        public string Key=>Reading==null?"":Reading.Provider+"|"+Board+"|"+Reading.ControllerId+"|"+Reading.Id+"|"+Reading.Hardware+"|"+Topology;
        public override string ToString()=>MissingLabel??(Reading==null?Localizer.T("Non associé"):$"{Reading.Hardware} · {Reading.Name} · {(Reading.Value is {} v?v.ToString("0",Localizer.Culture):"—")} {(Reading.Type=="Fan"?Localizer.T("tr/min"):"°C")}");
    }
    public MainWindow(Settings s,bool previewOnly)
    {
        SetLanguage(s.Language);InitializeComponent();WindowAppearance.Attach(this);this.previewOnly=previewOnly;editing=ConfigStore.Clone(s);running=ConfigStore.Clone(s);
        if(ConfigStore.IsInstalled){ActivateSensors.Content=Localizer.T("Vérifier la collecte");HeaderStatus.Text="VERSION 2.1";SensorHelp.Text=Localizer.T("Les capteurs sont lus automatiquement par le composant installé. Aucune modification des courbes de ventilation.");StorageHelp.Text=Localizer.T("Réglages et images enregistrés pour votre compte Windows. Le collecteur de capteurs est géré par l’installation.");}
        if(previewOnly){running.AutomaticFps=false;HeaderStatus.Text=Localizer.T("APERÇU · AUCUN ENVOI USB");}
        hub=new(running);hub.Start();
        tray=new(){Text=ConfigStore.IsInstalled?"LOU Control Center":"LOU Control Center · Portable",Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),Visible=true};
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();WindowState=WindowState.Normal;Activate();});
        var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add(Localizer.T("Ouvrir"),null,(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();}));menu.Items.Add(Localizer.T("Quitter"),null,(_,_)=>Dispatcher.Invoke(()=>Quit(this,new())));tray.ContextMenuStrip=menu;
        Closing+=(_,e)=>{if(closing)return;e.Cancel=true;if(running.MinimizeToTray&&!previewOnly){Hide();tray.ShowBalloonTip(1800,"LOU Control Center",Localizer.T("L’affichage continue. Utilisez Quitter pour libérer les écrans."),System.Windows.Forms.ToolTipIcon.Info);}else Quit(this,new());};
        SystemEvents.PowerModeChanged+=PowerChanged;
        Mode.ItemsSource=Labels.Modes;Mode.DisplayMemberPath="Label";Mode.SelectedValuePath="Value";
        FixedPage.ItemsSource=Labels.Pages;FixedPage.DisplayMemberPath="Label";FixedPage.SelectedValuePath="Value";
        PageEffect.ItemsSource=Labels.Transitions;PageEffect.DisplayMemberPath="Label";PageEffect.SelectedValuePath="Value";
        Rotation.ItemsSource=new[]{0,90,180,270};
        Accent.ItemsSource=new[]{new Choice<string>("#00E5FF","Cyan"),new("#FF2D95","Magenta"),new("#9B5CFF",Localizer.T("Violet")),new("#68E4CF",Localizer.T("Menthe")),new("#FF9F3C","Orange")};Accent.DisplayMemberPath="Label";Accent.SelectedValuePath="Value";
        BackgroundPage.ItemsSource=Labels.Pages;BackgroundPage.DisplayMemberPath="Label";BackgroundPage.SelectedValuePath="Value";BackgroundPage.SelectedIndex=0;
        foreach(var role in Enum.GetValues<FanRole>()){
            var box=new StackPanel{Margin=new Thickness(0,0,0,20)};box.Children.Add(new TextBlock{Text=CoolingLabels.Label(role),FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(104,228,207))});
            var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(14)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var label=new TextBox{MaxLength=40,ToolTip=Localizer.T("Nom affiché sur les écrans")};var connector=new TextBox{MaxLength=40,ToolTip=Localizer.T("Connecteur de la carte mère (facultatif)")};Grid.SetColumn(connector,2);grid.Children.Add(label);grid.Children.Add(connector);box.Children.Add(grid);var combo=new ComboBox{ToolTip=Localizer.T("Capteur matériel à associer")};box.Children.Add(combo);var live=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(135,148,159)),FontSize=12};box.Children.Add(live);FanEditors.Children.Add(box);fanControls[role]=(label,connector,combo,live);
        }
        rgb=new Lou.Rgb.RgbPanel(Path.Combine(ConfigStore.Root,"rgb.json"),previewOnly,s.Language==AppLanguage.English);
        Tabs.Items.Add(new TabItem{Header="RGB",Content=rgb,Style=(Style)FindResource("NavigationItem")});
        RefreshLanguage();
        timer.Tick+=(_,_)=>Tick();timer.Start();Loaded+=async(_,_)=>{await ScanAsync();if(running.HardwareMonitoring&&!previewOnly)StartSensors(this,new());if(!previewOnly&&running.CheckUpdatesOnStartup)await CheckUpdates(true);await rgb.StartAsync();};
    }
    private void FillGlobal(){filling=true;LanguageChoice.SelectedValue=editing.Language;AutoUpdate.IsChecked=editing.CheckUpdatesOnStartup;Accent.SelectedValue=editing.Accent;Glow.Value=editing.Glow;Motion.Value=editing.Animation;Night.IsChecked=editing.Night;NightStart.Text=editing.NightStart.ToString();NightEnd.Text=editing.NightEnd.ToString();Tray.IsChecked=editing.MinimizeToTray;AutoFps.IsChecked=editing.AutomaticFps;IncludedGames.Text=string.Join(Environment.NewLine,editing.Games);IgnoredGames.Text=string.Join(Environment.NewLine,editing.IgnoredGames);Network.Text=editing.NetworkAdapter;CityQuery.Text=editing.Location;CityStatus.Text=editing.CitySelected?Localizer.T("Ville choisie : ")+editing.Location:Localizer.T("Choisissez votre ville pour activer la météo.");filling=false;}
    private void RefreshDisplayChoices(){filling=true;string? id=selected?.InstanceId,previewId=(PreviewChoice.SelectedItem as DisplaySettings)?.InstanceId;DisplayChoice.ItemsSource=editing.Displays.ToArray();DisplayChoice.SelectedItem=editing.Displays.FirstOrDefault(d=>d.InstanceId==id)??editing.Displays.FirstOrDefault();PreviewChoice.ItemsSource=editing.Displays.ToArray();PreviewChoice.SelectedItem=editing.Displays.FirstOrDefault(d=>d.InstanceId==previewId)??editing.Displays.FirstOrDefault();selected=DisplayChoice.SelectedItem as DisplaySettings;filling=false;FillDisplay();}
    private void FillDisplay(){filling=true;DisplayEditor.IsEnabled=selected!=null;EmptyScreens.Visibility=selected==null?Visibility.Visible:Visibility.Collapsed;if(selected is {} d){DisplayName.Text=d.Name;DisplayEnabled.IsChecked=d.Enabled;Rotation.SelectedItem=d.Rotation;PortraitTextSize.Value=d.PortraitTextPercent;Fps.Text=d.UsbFps.ToString();Mode.SelectedValue=d.Mode;FixedPage.SelectedValue=d.FixedPage;Interval.Text=d.PageIntervalSeconds.ToString();PageEffect.SelectedValue=d.Pages.OrderBy(p=>p.Order).First().Transition;CoolingOnly.IsChecked=d.CoolingOnly;TransitionTime.Text=d.TransitionSeconds.ToString(Localizer.Culture);Quality.Text=d.JpegQuality.ToString();Pages.ItemsSource=d.Pages.OrderBy(p=>p.Order).ToArray();DeviceInfo.Text=d.Profile is {} p?$"{p.Width} × {p.Height} · {d.InstanceId}":d.InstanceId;}filling=false;}
    private static int Number(TextBox field)=>int.TryParse(field.Text,out int v)?v:throw new InvalidDataException(Localizer.T("Saisissez un nombre entier valide."));
    private void ReadDisplay(){if(selected==null)return;Pages.CommitEdit(DataGridEditingUnit.Cell,true);Pages.CommitEdit(DataGridEditingUnit.Row,true);var d=selected;d.Name=DisplayName.Text.Trim();d.Enabled=DisplayEnabled.IsChecked==true;d.Rotation=(int?)Rotation.SelectedItem??0;d.PortraitTextPercent=(int)Math.Round(PortraitTextSize.Value);d.UsbFps=Number(Fps);d.PreviewFps=d.UsbFps;d.Mode=(RotationMode?)Mode.SelectedValue??RotationMode.Automatic;d.FixedPage=(PageId?)FixedPage.SelectedValue??PageId.System;d.PageIntervalSeconds=Number(Interval);d.CoolingOnly=CoolingOnly.IsChecked==true;d.JpegQuality=Number(Quality);d.TransitionSeconds=double.Parse(TransitionTime.Text,Localizer.Culture);foreach(var p in d.Pages)p.Transition=(Transition?)PageEffect.SelectedValue??Transition.Slide;d.ValidatePlayback();}
    private void DisplayChanged(object sender,SelectionChangedEventArgs e){if(filling)return;try{ReadDisplay();selected=DisplayChoice.SelectedItem as DisplaySettings;FillDisplay();}catch(Exception ex){Status.Text=ex.Message;filling=true;DisplayChoice.SelectedItem=selected;filling=false;}}
    private void PreviewChanged(object sender,SelectionChangedEventArgs e){if(!filling)UpdatePreview();}
    private async void ScanClick(object sender,RoutedEventArgs e)=>await ScanAsync();
    private async Task ScanAsync()
    {
        if(scanning||disposed)return;scanning=true;
        try{
            var devices=await Task.Run(DeviceDiscovery.Scan);if(disposed)return;bool added=false;
            foreach(var usb in devices){if(editing.Displays.Any(d=>d.InstanceId.Equals(usb.InstanceId,StringComparison.OrdinalIgnoreCase)))continue;var d=new DisplaySettings{InstanceId=usb.InstanceId,Name=DisplayFamilies.Label(usb.Family)+(usb.CanStream?"":" · "+Localizer.T("Transport non pris en charge")),Enabled=usb.CanStream,PreviewWidth=usb.IsLy?1920:480,PreviewHeight=usb.IsLy?462:480,UsbFps=usb.IsLy?24:10};editing.Displays.Add(d);running.Displays.Add(ConfigStore.Clone(d));added=true;}
            if(previewOnly&&editing.Displays.Count==0){editing.Displays.Add(new(){InstanceId="preview-wide",Name=Localizer.T("Aperçu panoramique")});editing.Displays.Add(new(){InstanceId="preview-square",Name=Localizer.T("Aperçu carré"),PreviewWidth=480,PreviewHeight=480,UsbFps=10});running.Displays=ConfigStore.Clone(editing.Displays);added=true;}
            foreach(var d in running.Displays)EnsureHost(d);
            if(added){RefreshDisplayChoices();if(!previewOnly)ConfigStore.Save(running);}
            Status.Text=devices.Length==0?Localizer.T("Aucun écran compatible détecté. Branchez-le puis relancez la recherche."):Localizer.F($"{devices.Length} écran(s) détecté(s).");
        }catch(Exception ex){Status.Text=Localizer.T("Détection : ")+ex.Message;}finally{scanning=false;}
    }
    private void EnsureHost(DisplaySettings d)
    {
        if(hosts.ContainsKey(d.InstanceId))return;
        var host=new DashboardHost(running,d,()=>hub.Snapshot,previewOnly);hosts[d.InstanceId]=host;
        host.FrameReady+=b=>frames[d.InstanceId]=b;
        host.ProfileDetected+=p=>Dispatcher.BeginInvoke(()=>{if(disposed)return;var saved=running.Displays.SingleOrDefault(x=>x.InstanceId==p.InstanceId);var draft=editing.Displays.SingleOrDefault(x=>x.InstanceId==p.InstanceId);if(saved==null||draft==null)return;bool first=saved.Profile==null;saved.Profile=p;draft.Profile=p;if(first){saved.Rotation=p.Rotation;draft.Rotation=p.Rotation;if(selected?.InstanceId==p.InstanceId){filling=true;Rotation.SelectedItem=p.Rotation;filling=false;}}try{if(!previewOnly)ConfigStore.Save(running);}catch(Exception ex){Status.Text=ex.Message;}});
        host.Start();
    }
    private void Tick()
    {
        if(disposed)return;UpdatePreview();if(++ticks%5!=0)return;var s=hub.Snapshot;
        CpuValue.Text=NeonRenderer.V(s.Pc.CpuTemp," °C")+" · "+NeonRenderer.V(s.Pc.Cpu," %");GpuValue.Text=NeonRenderer.V(s.Pc.GpuTemp," °C")+" · "+NeonRenderer.V(s.Pc.Gpu," %");PumpValue.Text=NeonRenderer.V(s.Pc.Pump,Localizer.T(" tr/min"));
        var raw=Inventory();foreach(var control in fanControls.Values){var option=control.Sensor.SelectedItem as SensorOption;var matches=raw.Where(r=>r.Key==option?.Key).ToArray();var current=matches.Length==1?matches[0]:null;control.Live.Text=option?.Reading==null?Localizer.T("À associer"):current==null?Localizer.T("Capteur absent ou inventaire modifié — sélectionnez à nouveau la mesure."):Localizer.T("Lecture actuelle : ")+NeonRenderer.V(current.Reading!.Value,Localizer.T(" tr/min"));}
        SensorStatus.Text=Localizer.T(s.Pc.CoolingStatus);SetupSummary.Text=Localizer.F($"{editing.Displays.Count} écran(s) · {editing.FanBindings.Count} capteur(s) associé(s) · {(editing.CitySelected?editing.Location:Localizer.T("Ville à choisir"))}");
        if(ticks%50==0)_=ScanAsync();
        if(ticks%25==0)try{var info=new{Time=DateTimeOffset.Now,Pid=Environment.ProcessId,Version=AppVersion,PreviewOnly=previewOnly,ConfigRoot=ConfigStore.Root,Displays=hosts.Select(h=>new{Id=h.Key,h.Value.Status,h.Value.CurrentPage,h.Value.RenderedFrames,h.Value.Frames,h.Value.UniqueFrames,h.Value.UsbActualFps,h.Value.RenderMs,h.Value.EncodeMs}),Cooling=s.Pc.Fans};File.WriteAllText(Path.Combine(ConfigStore.Root,previewOnly?"preview-status.json":"runtime-status.json"),JsonSerializer.Serialize(info,ConfigStore.Json));}catch{}
    }
    private void UpdatePreview(){if(PreviewChoice.SelectedItem is not DisplaySettings d)return;if(frames.TryGetValue(d.InstanceId,out var b)){Preview.MaxHeight=b.PixelHeight>b.PixelWidth?640:320;Preview.Source=b;}PreviewTitle.Text=d.Name;if(hosts.TryGetValue(d.InstanceId,out var h))PreviewStatus.Text=Labels.Page(h.CurrentPage)+" · "+(previewOnly?Localizer.T("Aperçu uniquement"):Localizer.T(h.Status));}
    private List<SensorOption> Inventory(){var s=HardwareSensors.Read();var rows=SensorCatalog.Fresh(s,DateTimeOffset.UtcNow)?s!.Readings:[];var list=rows.Where(r=>r.Type is "Fan" or "Temperature").Select(r=>new SensorOption(r,s!.Board??Localizer.T("Carte inconnue"),s.Topology)).ToList();if(hub.GpuFanSensor is {} gpu)list.Add(new(gpu,gpu.ParentName));return list;}
    private static SensorOption Missing(FanBinding b)=>new(new(b.SensorId,b.Hardware,"",b.SensorId,"Fan",null,b.ControllerId,Provider:b.Provider),b.Board,b.Topology,Localizer.T("Capteur associé absent · ")+b.SensorId);
    private void FillSensors(bool preserve)
    {
        var list=Inventory();foreach(var (role,controls) in fanControls){var old=preserve?controls.Sensor.SelectedItem as SensorOption:null;var binding=editing.FanBindings.SingleOrDefault(b=>b.Role==role);var options=new List<SensorOption>{new(null)};options.AddRange(list.Where(s=>s.Reading!.Type=="Fan"&&(role==FanRole.Gpu?s.Reading.Kind.StartsWith("Gpu"):!s.Reading.Kind.StartsWith("Gpu"))));var target=old??(binding==null?null:Missing(binding));if(target?.Reading!=null&&!options.Any(o=>o.Key==target.Key))options.Add(target);controls.Sensor.ItemsSource=options;controls.Sensor.SelectedItem=options.FirstOrDefault(o=>o.Key==target?.Key)??options[0];if(!preserve){controls.Label.Text=Localizer.FanLabel(editing,role);controls.Connector.Text=binding?.Connector??"";}}
        FillTemperature(CpuTempChoice,editing.CpuTemperature,list,preserve);FillTemperature(GpuTempChoice,editing.GpuTemperature,list,preserve);
        var data=HardwareSensors.Read();var gpus=(SensorCatalog.Fresh(data,DateTimeOffset.UtcNow)?data!.Readings:[]).Where(r=>r.Kind.StartsWith("Gpu")).Select(r=>new GpuOption(r.ControllerId,r.Hardware,r.DeviceId)).Distinct().ToList();gpus.Insert(0,new("",""));var chosen=preserve&&GpuChoice.SelectedItem is GpuOption oldGpu?oldGpu:new(editing.GpuController,editing.GpuHardware,editing.GpuDeviceId);if(!gpus.Contains(chosen))gpus.Add(chosen);GpuChoice.ItemsSource=gpus;GpuChoice.SelectedItem=chosen;
        SensorDetails.Text=string.Join("\n\n",list.Select(o=>Localizer.F($"{o.Reading!.Hardware} / {o.Reading.Name} : {o.Reading.Value}\n{o.Reading.Provider} · {o.Board}\nContrôleur : {o.Reading.ControllerId}\nParent : {o.Reading.ParentId} · {o.Reading.ParentName}\nCapteur : {o.Reading.Id}\\nIdentité physique : {o.Reading.DeviceId}")));
    }
    private static void FillTemperature(ComboBox box,SensorBinding? binding,List<SensorOption> list,bool preserve){var old=preserve?box.SelectedItem as SensorOption:null;var options=new List<SensorOption>{new(null,MissingLabel:Localizer.T("Automatique"))};options.AddRange(list.Where(o=>o.Reading!.Type=="Temperature"));var target=old??(binding==null?null:new SensorOption(new(binding.SensorId,binding.Hardware,"",binding.SensorId,"Temperature",null,binding.ControllerId,Provider:binding.Provider),binding.Board,binding.Topology,Localizer.T("Capteur associé absent · ")+binding.SensorId));if(target?.Reading!=null&&!options.Any(o=>o.Key==target.Key))options.Add(target);box.ItemsSource=options;box.SelectedItem=options.FirstOrDefault(o=>o.Key==target?.Key)??options[0];}
    private static SensorBinding? TemperatureBinding(ComboBox box)=>box.SelectedItem is SensorOption {Reading:{} r} o?new(r.Provider,o.Board,r.ControllerId,r.Id,r.Hardware,o.Topology):null;
    private void ReadAll()
    {
        ReadDisplay();editing.FanBindings=[];editing.FanLabels=[];foreach(var (role,control) in fanControls){if(!string.IsNullOrWhiteSpace(control.Label.Text)&&!Localizer.IsDefaultFanLabel(role,control.Label.Text.Trim()))editing.FanLabels[role]=control.Label.Text.Trim();if(control.Sensor.SelectedItem is SensorOption{Reading:{} r} o)editing.FanBindings.Add(new(role,r.Provider,o.Board,r.ControllerId,r.Id,control.Connector.Text.Trim(),Localizer.T("Association utilisateur"),r.Hardware,o.Topology));}
        editing.CpuTemperature=TemperatureBinding(CpuTempChoice);editing.GpuTemperature=TemperatureBinding(GpuTempChoice);if(GpuChoice.SelectedItem is GpuOption gpu){editing.GpuController=gpu.Id;editing.GpuHardware=gpu.Hardware;editing.GpuDeviceId=gpu.DeviceId;}
        editing.Accent=(string?)Accent.SelectedValue??"#00E5FF";editing.Glow=Glow.Value;editing.Animation=Motion.Value;editing.Night=Night.IsChecked==true;editing.NightStart=Number(NightStart);editing.NightEnd=Number(NightEnd);editing.MinimizeToTray=Tray.IsChecked==true;editing.AutomaticFps=AutoFps.IsChecked==true;editing.NetworkAdapter=Network.Text.Trim();editing.Games=Lines(IncludedGames.Text);editing.IgnoredGames=Lines(IgnoredGames.Text);editing.Language=(AppLanguage?)LanguageChoice.SelectedValue??AppLanguage.French;editing.CheckUpdatesOnStartup=AutoUpdate.IsChecked==true;editing.Validate();
    }
    private static string[] Lines(string s)=>s.Split(['\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFileNameWithoutExtension).Where(s=>!string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()!;
    private async void Save(object sender,RoutedEventArgs e){try{await SaveSettings();}catch(Exception ex){Status.Text=ex.Message;}}
    private async Task SaveSettings(){ReadAll();if(previewOnly)ConfigStore.Save(editing,Path.Combine(ConfigStore.Root,"preview-settings.json"));else ConfigStore.Save(editing);running=ConfigStore.Clone(editing);if(previewOnly)running.AutomaticFps=false;hub.Update(running);foreach(string id in hosts.Keys.Except(running.Displays.Select(d=>d.InstanceId)).ToArray()){await hosts[id].DisposeAsync();hosts.Remove(id);frames.TryRemove(id,out _);}foreach(var d in running.Displays){if(hosts.TryGetValue(d.InstanceId,out var h))h.Update(running,d);else EnsureHost(d);}RefreshLanguage();Status.Text=previewOnly?Localizer.T("Réglages d’aperçu enregistrés séparément. Aucun envoi USB."):Localizer.T("Réglages enregistrés et appliqués.");}
    private void RefreshSensors(object sender,RoutedEventArgs e){FillSensors(true);Status.Text=Localizer.T("Inventaire actualisé. Les associations existantes sont conservées.");}
    private async void StartSensors(object sender,RoutedEventArgs e)
    {
        if(previewOnly){Status.Text=Localizer.T("Fermez l’aperçu et lancez normalement l’application pour activer ses capteurs.");return;}
        if(ConfigStore.IsInstalled){FillSensors(true);var data=HardwareSensors.Read();Status.Text=data!=null&&DateTimeOffset.UtcNow-data.Time<TimeSpan.FromSeconds(5)?Localizer.T("Collecte installée : ")+Localizer.T(data.Status):Localizer.T("Collecte indisponible. Relancez l’installateur pour réparer les composants, puis redémarrez Windows si demandé.");return;}
        try{
            if(!LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled){string installer=Path.Combine(AppContext.BaseDirectory,"tools","PawnIO","PawnIO_setup.exe");if(!File.Exists(installer))throw new FileNotFoundException(Localizer.T("Composant PawnIO absent du dossier portable."));if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(installer)))!="1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032")throw new InvalidDataException(Localizer.T("Composant PawnIO invalide."));using var setup=Process.Start(new ProcessStartInfo(installer){UseShellExecute=true,Verb="runas"});if(setup!=null)await setup.WaitForExitAsync();if(!LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled){Status.Text=Localizer.T("PawnIO n’est pas installé. Les autres fonctions restent disponibles.");return;}}
            Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Lou.ControlCenter.exe"),"--sensor-collector --parent-pid "+Environment.ProcessId){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});editing.HardwareMonitoring=true;running.HardwareMonitoring=true;ConfigStore.Save(running);Status.Text=Localizer.T("Autorisation demandée à Windows pour la lecture des capteurs.");await Task.Delay(4000);if(!disposed)FillSensors(true);
        }catch(Exception ex){Status.Text=Localizer.T("Activation : ")+ex.Message;}
    }
    private async void SearchCity(object sender,RoutedEventArgs e){searchStop.Cancel();searchStop.Dispose();searchStop=new();var token=searchStop.Token;try{CityStatus.Text=Localizer.T("Recherche…");var results=await cities.SearchAsync(CityQuery.Text,token);if(token.IsCancellationRequested)return;CityResults.ItemsSource=results;CityResults.SelectedIndex=results.Length>0?0:-1;CityStatus.Text=results.Length>0?Localizer.T("Choisissez la ville et le pays correspondants."):Localizer.T("Aucun résultat. Essayez le nom complet de la ville.");}catch(OperationCanceledException){}catch(Exception ex){CityStatus.Text=Localizer.T("Recherche indisponible : ")+ex.Message;}}
    private void CityKey(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==Key.Enter)SearchCity(sender,new());}
    private void SelectCity(object sender,RoutedEventArgs e){if(CityResults.SelectedItem is not City city)return;editing.Location=city.Label;editing.Latitude=city.Latitude;editing.Longitude=city.Longitude;editing.CitySelected=true;CityStatus.Text=Localizer.T("Ville choisie : ")+city.Label+Localizer.T(". Enregistrez pour l’appliquer.");}
    private void TestDisplay(object sender,RoutedEventArgs e){if(selected!=null&&hosts.TryGetValue(selected.InstanceId,out var h)){h.Calibration=!h.Calibration;Status.Text=h.Calibration?Localizer.T("Mire active. Réglez l’orientation et enregistrez, puis arrêtez la mire."):Localizer.T("Affichage normal rétabli.");}}
    private void ResetDisplay(object sender,RoutedEventArgs e){if(selected!=null){selected.Profile=null;Status.Text=Localizer.T("Enregistrez pour relire le profil matériel de cet écran.");}}
    private void ChooseBackground(object sender,RoutedEventArgs e){try{var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.webp"};if(dialog.ShowDialog()!=true)return;var page=(PageId?)BackgroundPage.SelectedValue??PageId.System;using(var codec=SkiaSharp.SKCodec.Create(dialog.FileName)){if(codec==null||codec.Info.Width>8192||codec.Info.Height>8192)throw new InvalidDataException(Localizer.T("Image invalide ou trop grande (8 192 pixels maximum)."));}string folder=Path.Combine(ConfigStore.Root,"Backgrounds");Directory.CreateDirectory(folder);string name=page+"-"+Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName);File.Copy(dialog.FileName,Path.Combine(folder,name));editing.Backgrounds[page]=Path.Combine("Data","Backgrounds",name);BackgroundInfo.Text=Localizer.T("Décor copié dans le dossier des données. Enregistrez pour l’appliquer.");}catch(Exception ex){Status.Text=ex.Message;}}
    private void ResetBackground(object sender,RoutedEventArgs e){editing.Backgrounds.Remove((PageId?)BackgroundPage.SelectedValue??PageId.System);BackgroundInfo.Text=Localizer.T("Décor d’origine sélectionné. Enregistrez pour l’appliquer.");}
    private void ExportConfig(object sender,RoutedEventArgs e){try{ReadAll();var dialog=new SaveFileDialog{Filter=Localizer.T("Configuration LOU|*.json"),FileName=Localizer.T("LOU-reglages.json")};if(dialog.ShowDialog()==true){ConfigStore.Save(editing,dialog.FileName);Status.Text=Localizer.T("Configuration exportée. Les images personnalisées restent dans Data/Backgrounds.");}}catch(Exception ex){Status.Text=ex.Message;}}
    private void ImportConfig(object sender,RoutedEventArgs e){try{var dialog=new OpenFileDialog{Filter=Localizer.T("Configuration LOU|*.json")};if(dialog.ShowDialog()==true){editing=ConfigStore.Load(dialog.FileName);selected=null;FillGlobal();FillSensors(false);RefreshDisplayChoices();Status.Text=Localizer.T("Configuration chargée. Vérifiez les associations puis enregistrez pour l’appliquer.");}}catch(Exception ex){Status.Text=ex.Message;}}
    private void OpenData(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("explorer.exe",ConfigStore.Root){UseShellExecute=true});
    private void GoScreens(object sender,RoutedEventArgs e)=>Tabs.SelectedIndex=1;
    private void GoSensors(object sender,RoutedEventArgs e)=>Tabs.SelectedIndex=2;
    private void GoWeather(object sender,RoutedEventArgs e)=>Tabs.SelectedIndex=3;
    private void ScrollPages(object sender,MouseWheelEventArgs e){e.Handled=true;var parent=System.Windows.Media.VisualTreeHelper.GetParent(Pages);while(parent!=null&&parent is not ScrollViewer)parent=System.Windows.Media.VisualTreeHelper.GetParent(parent);if(parent is ScrollViewer scroll)scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta);}
    private void PowerChanged(object sender,PowerModeChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{foreach(var h in hosts.Values)if(e.Mode==PowerModes.Suspend)h.Suspend();else if(e.Mode==PowerModes.Resume)h.Resume();});
    private async void Quit(object sender,RoutedEventArgs e){if(closing)return;closing=true;await ShutdownHost();Close();System.Windows.Application.Current.Shutdown();}
    public async Task ShutdownHost(){if(disposed)return;disposed=true;rgb?.Dispose();updateStop.Cancel();updates.Dispose();timer.Stop();SystemEvents.PowerModeChanged-=PowerChanged;searchStop.Cancel();tray.Visible=false;tray.Dispose();foreach(var h in hosts.Values)await h.DisposeAsync();await hub.DisposeAsync();cities.Dispose();searchStop.Dispose();}
    private void Capture(string path){UpdateLayout();var image=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(this);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);png.Save(file);}
    public async Task CaptureReview(){
        string folder=Path.Combine(ConfigStore.IsInstalled?ConfigStore.Root:AppContext.BaseDirectory,"Review");Directory.CreateDirectory(folder);
        for(int i=0;i<Tabs.Items.Count;i++){Tabs.SelectedIndex=i;await Task.Delay(350);Capture(Path.Combine(folder,$"ui-{i}.png"));}
        if(previewOnly&&editing.Displays.Count>1){
            Tabs.SelectedIndex=1;DisplayChoice.SelectedIndex=0;Interval.Text="27";PortraitTextSize.Value=150;await SaveSettings();var saved=ConfigStore.Load(Path.Combine(ConfigStore.Root,"preview-settings.json"));if(saved.Displays.Count<2||saved.Displays[0].PageIntervalSeconds!=27||saved.Displays[1].PageIntervalSeconds!=15||saved.Displays[0].PortraitTextPercent!=150||saved.Displays[1].PortraitTextPercent!=125)throw new InvalidOperationException("L’isolation du délai par écran a échoué.");
            DisplayChoice.SelectedIndex=1;if(Interval.Text!="15"||PortraitTextSize.Value!=125)throw new InvalidOperationException("La sélection de l’écran perd ses réglages.");
            Tabs.SelectedIndex=0;PreviewChoice.SelectedIndex=1;await Task.Delay(500);Capture(Path.Combine(folder,"ui-square.png"));
        }
        if(previewOnly){
            var original=running.Language;
            foreach(var language in new[]{AppLanguage.French,AppLanguage.English,original}){
                LanguageChoice.SelectedValue=language;await SaveSettings();
                var saved=ConfigStore.Load(Path.Combine(ConfigStore.Root,"preview-settings.json"));
                if(saved.Language!=language||hosts.Values.Any(h=>h.Settings.Language!=language))throw new InvalidOperationException("Language did not reach settings and display workers.");
                Tabs.SelectedIndex=5;await Task.Delay(200);Capture(Path.Combine(folder,"settings-"+language+".png"));
            }
            await CheckUpdates(false);Capture(Path.Combine(folder,"updates-live.png"));
        }
        Width=1120;Height=740;await Task.Delay(350);Capture(Path.Combine(folder,"ui-compact.png"));
        File.WriteAllText(Path.Combine(folder,"ui-verification.json"),JsonSerializer.Serialize(new{Time=DateTimeOffset.Now,PreviewOnly=previewOnly,Displays=editing.Displays.Count,IndependentDelay=true,IndependentPortraitText=true,UsbFrames=hosts.Values.Sum(h=>h.Frames)},ConfigStore.Json));
    }
    public async Task VerifyRuntime(){
        string folder=Path.Combine(ConfigStore.IsInstalled?ConfigStore.Root:AppContext.BaseDirectory,"Review");Directory.CreateDirectory(folder);
        foreach(var d in running.Displays){var test=ConfigStore.Clone(d);test.Mode=RotationMode.Automatic;test.PageIntervalSeconds=3;hosts[d.InstanceId].Update(running,test);}
        var start=DateTimeOffset.UtcNow;var before=hosts.ToDictionary(h=>h.Key,h=>(h.Value.Frames,h.Value.UniqueFrames,h.Value.RenderedFrames));var pages=hosts.Keys.ToDictionary(k=>k,k=>new HashSet<PageId>());var samples=new List<object>();
        for(int i=0;i<35;i++){await Task.Delay(1000);foreach(var h in hosts)pages[h.Key].Add(h.Value.CurrentPage);samples.Add(new{Seconds=(DateTimeOffset.UtcNow-start).TotalSeconds,Displays=hosts.Select(h=>new{Id=h.Key,h.Value.Status,h.Value.CurrentPage,h.Value.RenderMs,h.Value.EncodeMs,h.Value.UsbMs,h.Value.Frames,h.Value.UniqueFrames}).ToArray()});if(i is >=12 and <=14)Thread.Sleep(250);}
        double seconds=(DateTimeOffset.UtcNow-start).TotalSeconds;var result=new{Time=DateTimeOffset.Now,Seconds=seconds,Displays=hosts.Select(h=>new{Id=h.Key,h.Value.Status,TransmittedFps=(h.Value.Frames-before[h.Key].Frames)/seconds,UniqueFps=(h.Value.UniqueFrames-before[h.Key].UniqueFrames)/seconds,RenderedFps=(h.Value.RenderedFrames-before[h.Key].RenderedFrames)/seconds,Pages=pages[h.Key],h.Value.RenderMs,h.Value.EncodeMs}).ToArray(),Samples=samples};File.WriteAllText(Path.Combine(folder,"physical-verification.json"),JsonSerializer.Serialize(result,ConfigStore.Json));
        Tabs.SelectedIndex=0;Capture(Path.Combine(folder,"physical-ui.png"));
    }
}



