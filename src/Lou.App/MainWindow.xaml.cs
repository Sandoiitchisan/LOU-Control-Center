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
    private readonly record struct GpuOption(string Id,string Hardware,string DeviceId=""){public override string ToString()=>Id==""?"Automatique (si une seule carte est disponible)":Hardware+" · "+Id;}
    private sealed record SensorOption(HardwareReading? Reading,string Board="",string Topology="",string? MissingLabel=null)
    {
        public string Key=>Reading==null?"":Reading.Provider+"|"+Board+"|"+Reading.ControllerId+"|"+Reading.Id+"|"+Reading.Hardware+"|"+Topology;
        public override string ToString()=>MissingLabel??(Reading==null?"Non associé":$"{Reading.Hardware} · {Reading.Name} · {(Reading.Value is {} v?v.ToString("0",CultureInfo.CurrentCulture):"—")} {(Reading.Type=="Fan"?"tr/min":"°C")}");
    }
    public MainWindow(Settings s,bool previewOnly)
    {
        InitializeComponent();WindowAppearance.Attach(this);this.previewOnly=previewOnly;editing=ConfigStore.Clone(s);running=ConfigStore.Clone(s);
        if(ConfigStore.IsInstalled){ActivateSensors.Content="Vérifier la collecte";HeaderStatus.Text="VERSION 2.1";SensorHelp.Text="Les capteurs sont lus automatiquement par le composant installé. Aucune modification des courbes de ventilation.";StorageHelp.Text="Réglages et images enregistrés pour votre compte Windows. Le collecteur de capteurs est géré par l’installation.";}
        if(previewOnly){running.AutomaticFps=false;HeaderStatus.Text="APERÇU · AUCUN ENVOI USB";}
        hub=new(running);hub.Start();
        tray=new(){Text=ConfigStore.IsInstalled?"LOU Control Center":"LOU Control Center · Portable",Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),Visible=true};
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();WindowState=WindowState.Normal;Activate();});
        var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("Ouvrir",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Activate();}));menu.Items.Add("Quitter",null,(_,_)=>Dispatcher.Invoke(()=>Quit(this,new())));tray.ContextMenuStrip=menu;
        Closing+=(_,e)=>{if(closing)return;e.Cancel=true;if(running.MinimizeToTray&&!previewOnly){Hide();tray.ShowBalloonTip(1800,"LOU Control Center","L’affichage continue. Utilisez Quitter pour libérer les écrans.",System.Windows.Forms.ToolTipIcon.Info);}else Quit(this,new());};
        SystemEvents.PowerModeChanged+=PowerChanged;
        Mode.ItemsSource=Labels.Modes;Mode.DisplayMemberPath="Label";Mode.SelectedValuePath="Value";
        FixedPage.ItemsSource=Labels.Pages;FixedPage.DisplayMemberPath="Label";FixedPage.SelectedValuePath="Value";
        PageEffect.ItemsSource=Labels.Transitions;PageEffect.DisplayMemberPath="Label";PageEffect.SelectedValuePath="Value";
        Rotation.ItemsSource=new[]{0,90,180,270};
        Accent.ItemsSource=new[]{new Choice<string>("#00E5FF","Cyan"),new("#FF2D95","Magenta"),new("#9B5CFF","Violet"),new("#68E4CF","Menthe"),new("#FF9F3C","Orange")};Accent.DisplayMemberPath="Label";Accent.SelectedValuePath="Value";
        BackgroundPage.ItemsSource=Labels.Pages;BackgroundPage.DisplayMemberPath="Label";BackgroundPage.SelectedValuePath="Value";BackgroundPage.SelectedIndex=0;
        foreach(var role in Enum.GetValues<FanRole>()){
            var box=new StackPanel{Margin=new Thickness(0,0,0,20)};box.Children.Add(new TextBlock{Text=CoolingLabels.Label(role),FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(104,228,207))});
            var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(14)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
            var label=new TextBox{MaxLength=40,ToolTip="Nom affiché sur les écrans"};var connector=new TextBox{MaxLength=40,ToolTip="Connecteur de la carte mère (facultatif)"};Grid.SetColumn(connector,2);grid.Children.Add(label);grid.Children.Add(connector);box.Children.Add(grid);var combo=new ComboBox{ToolTip="Capteur matériel à associer"};box.Children.Add(combo);var live=new TextBlock{Foreground=new SolidColorBrush(Color.FromRgb(135,148,159)),FontSize=12};box.Children.Add(live);FanEditors.Children.Add(box);fanControls[role]=(label,connector,combo,live);
        }
        FillGlobal();FillSensors(false);RefreshDisplayChoices();
        timer.Tick+=(_,_)=>Tick();timer.Start();Loaded+=async(_,_)=>{await ScanAsync();if(running.HardwareMonitoring&&!previewOnly)StartSensors(this,new());};
    }
    private void FillGlobal(){filling=true;Accent.SelectedValue=editing.Accent;Glow.Value=editing.Glow;Motion.Value=editing.Animation;Night.IsChecked=editing.Night;NightStart.Text=editing.NightStart.ToString();NightEnd.Text=editing.NightEnd.ToString();Tray.IsChecked=editing.MinimizeToTray;AutoFps.IsChecked=editing.AutomaticFps;IncludedGames.Text=string.Join(Environment.NewLine,editing.Games);IgnoredGames.Text=string.Join(Environment.NewLine,editing.IgnoredGames);Network.Text=editing.NetworkAdapter;CityQuery.Text=editing.Location;CityStatus.Text=editing.CitySelected?"Ville choisie : "+editing.Location:"Choisissez votre ville pour activer la météo.";filling=false;}
    private void RefreshDisplayChoices(){filling=true;string? id=selected?.InstanceId,previewId=(PreviewChoice.SelectedItem as DisplaySettings)?.InstanceId;DisplayChoice.ItemsSource=editing.Displays.ToArray();DisplayChoice.SelectedItem=editing.Displays.FirstOrDefault(d=>d.InstanceId==id)??editing.Displays.FirstOrDefault();PreviewChoice.ItemsSource=editing.Displays.ToArray();PreviewChoice.SelectedItem=editing.Displays.FirstOrDefault(d=>d.InstanceId==previewId)??editing.Displays.FirstOrDefault();selected=DisplayChoice.SelectedItem as DisplaySettings;filling=false;FillDisplay();}
    private void FillDisplay(){filling=true;DisplayEditor.IsEnabled=selected!=null;EmptyScreens.Visibility=selected==null?Visibility.Visible:Visibility.Collapsed;if(selected is {} d){DisplayName.Text=d.Name;DisplayEnabled.IsChecked=d.Enabled;Rotation.SelectedItem=d.Rotation;Fps.Text=d.UsbFps.ToString();Mode.SelectedValue=d.Mode;FixedPage.SelectedValue=d.FixedPage;Interval.Text=d.PageIntervalSeconds.ToString();PageEffect.SelectedValue=d.Pages.OrderBy(p=>p.Order).First().Transition;CoolingOnly.IsChecked=d.CoolingOnly;TransitionTime.Text=d.TransitionSeconds.ToString(CultureInfo.CurrentCulture);Quality.Text=d.JpegQuality.ToString();Pages.ItemsSource=d.Pages.OrderBy(p=>p.Order).ToArray();DeviceInfo.Text=d.Profile is {} p?$"{p.Width} × {p.Height} · {d.InstanceId}":d.InstanceId;}filling=false;}
    private static int Number(TextBox field)=>int.TryParse(field.Text,out int v)?v:throw new InvalidDataException("Saisissez un nombre entier valide.");
    private void ReadDisplay(){if(selected==null)return;Pages.CommitEdit(DataGridEditingUnit.Cell,true);Pages.CommitEdit(DataGridEditingUnit.Row,true);var d=selected;d.Name=DisplayName.Text.Trim();d.Enabled=DisplayEnabled.IsChecked==true;d.Rotation=(int?)Rotation.SelectedItem??0;d.UsbFps=Number(Fps);d.PreviewFps=d.UsbFps;d.Mode=(RotationMode?)Mode.SelectedValue??RotationMode.Automatic;d.FixedPage=(PageId?)FixedPage.SelectedValue??PageId.System;d.PageIntervalSeconds=Number(Interval);d.CoolingOnly=CoolingOnly.IsChecked==true;d.JpegQuality=Number(Quality);d.TransitionSeconds=double.Parse(TransitionTime.Text,CultureInfo.CurrentCulture);foreach(var p in d.Pages)p.Transition=(Transition?)PageEffect.SelectedValue??Transition.Slide;d.ValidatePlayback();}
    private void DisplayChanged(object sender,SelectionChangedEventArgs e){if(filling)return;try{ReadDisplay();selected=DisplayChoice.SelectedItem as DisplaySettings;FillDisplay();}catch(Exception ex){Status.Text=ex.Message;filling=true;DisplayChoice.SelectedItem=selected;filling=false;}}
    private void PreviewChanged(object sender,SelectionChangedEventArgs e){if(!filling)UpdatePreview();}
    private async void ScanClick(object sender,RoutedEventArgs e)=>await ScanAsync();
    private async Task ScanAsync()
    {
        if(scanning||disposed)return;scanning=true;
        try{
            var devices=await Task.Run(DeviceDiscovery.Scan);if(disposed)return;bool added=false;
            foreach(var usb in devices){if(editing.Displays.Any(d=>d.InstanceId==usb.InstanceId))continue;var d=new DisplaySettings{InstanceId=usb.InstanceId,Name=usb.IsLy?"Écran panoramique":"Écran du watercooling",PreviewWidth=usb.IsLy?1920:480,PreviewHeight=usb.IsLy?462:480,UsbFps=usb.IsLy?24:10};editing.Displays.Add(d);running.Displays.Add(ConfigStore.Clone(d));added=true;}
            if(previewOnly&&editing.Displays.Count==0){editing.Displays.Add(new(){InstanceId="preview-wide",Name="Aperçu panoramique"});editing.Displays.Add(new(){InstanceId="preview-square",Name="Aperçu carré",PreviewWidth=480,PreviewHeight=480,UsbFps=10});running.Displays=ConfigStore.Clone(editing.Displays);added=true;}
            foreach(var d in running.Displays)EnsureHost(d);
            if(added){RefreshDisplayChoices();if(!previewOnly)ConfigStore.Save(running);}
            Status.Text=devices.Length==0?"Aucun écran compatible détecté. Branchez-le puis relancez la recherche.":$"{devices.Length} écran(s) détecté(s).";
        }catch(Exception ex){Status.Text="Détection : "+ex.Message;}finally{scanning=false;}
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
        CpuValue.Text=NeonRenderer.V(s.Pc.CpuTemp," °C")+" · "+NeonRenderer.V(s.Pc.Cpu," %");GpuValue.Text=NeonRenderer.V(s.Pc.GpuTemp," °C")+" · "+NeonRenderer.V(s.Pc.Gpu," %");PumpValue.Text=NeonRenderer.V(s.Pc.Pump," tr/min");
        var raw=Inventory();foreach(var control in fanControls.Values){var option=control.Sensor.SelectedItem as SensorOption;var matches=raw.Where(r=>r.Key==option?.Key).ToArray();var current=matches.Length==1?matches[0]:null;control.Live.Text=option?.Reading==null?"À associer":current==null?"Capteur absent ou inventaire modifié — sélectionnez à nouveau la mesure.":"Lecture actuelle : "+NeonRenderer.V(current.Reading!.Value," tr/min");}
        SensorStatus.Text=s.Pc.CoolingStatus;SetupSummary.Text=$"{editing.Displays.Count} écran(s) · {editing.FanBindings.Count} capteur(s) associé(s) · {(editing.CitySelected?editing.Location:"Ville à choisir")}";
        if(ticks%50==0)_=ScanAsync();
        if(ticks%25==0)try{var info=new{Time=DateTimeOffset.Now,Pid=Environment.ProcessId,Version="2.1.0",PreviewOnly=previewOnly,ConfigRoot=ConfigStore.Root,Displays=hosts.Select(h=>new{Id=h.Key,h.Value.Status,h.Value.CurrentPage,h.Value.RenderedFrames,h.Value.Frames,h.Value.UniqueFrames,h.Value.UsbActualFps,h.Value.RenderMs,h.Value.EncodeMs}),Cooling=s.Pc.Fans};File.WriteAllText(Path.Combine(ConfigStore.Root,previewOnly?"preview-status.json":"runtime-status.json"),JsonSerializer.Serialize(info,ConfigStore.Json));}catch{}
    }
    private void UpdatePreview(){if(PreviewChoice.SelectedItem is not DisplaySettings d)return;if(frames.TryGetValue(d.InstanceId,out var b))Preview.Source=b;PreviewTitle.Text=d.Name;if(hosts.TryGetValue(d.InstanceId,out var h))PreviewStatus.Text=Labels.Page(h.CurrentPage)+" · "+(previewOnly?"Aperçu uniquement":h.Status);}
    private List<SensorOption> Inventory(){var s=HardwareSensors.Read();var rows=SensorCatalog.Fresh(s,DateTimeOffset.UtcNow)?s!.Readings:[];var list=rows.Where(r=>r.Type is "Fan" or "Temperature").Select(r=>new SensorOption(r,s!.Board??"Carte inconnue",s.Topology)).ToList();if(hub.GpuFanSensor is {} gpu)list.Add(new(gpu,gpu.ParentName));return list;}
    private static SensorOption Missing(FanBinding b)=>new(new(b.SensorId,b.Hardware,"",b.SensorId,"Fan",null,b.ControllerId,Provider:b.Provider),b.Board,b.Topology,"Capteur associé absent · "+b.SensorId);
    private void FillSensors(bool preserve)
    {
        var list=Inventory();foreach(var (role,controls) in fanControls){var old=preserve?controls.Sensor.SelectedItem as SensorOption:null;var binding=editing.FanBindings.SingleOrDefault(b=>b.Role==role);var options=new List<SensorOption>{new(null)};options.AddRange(list.Where(s=>s.Reading!.Type=="Fan"&&(role==FanRole.Gpu?s.Reading.Kind.StartsWith("Gpu"):!s.Reading.Kind.StartsWith("Gpu"))));var target=old??(binding==null?null:Missing(binding));if(target?.Reading!=null&&!options.Any(o=>o.Key==target.Key))options.Add(target);controls.Sensor.ItemsSource=options;controls.Sensor.SelectedItem=options.FirstOrDefault(o=>o.Key==target?.Key)??options[0];if(!preserve){controls.Label.Text=editing.FanLabels.GetValueOrDefault(role,CoolingLabels.Label(role));controls.Connector.Text=binding?.Connector??"";}}
        FillTemperature(CpuTempChoice,editing.CpuTemperature,list,preserve);FillTemperature(GpuTempChoice,editing.GpuTemperature,list,preserve);
        var data=HardwareSensors.Read();var gpus=(SensorCatalog.Fresh(data,DateTimeOffset.UtcNow)?data!.Readings:[]).Where(r=>r.Kind.StartsWith("Gpu")).Select(r=>new GpuOption(r.ControllerId,r.Hardware,r.DeviceId)).Distinct().ToList();gpus.Insert(0,new("",""));var chosen=preserve&&GpuChoice.SelectedItem is GpuOption oldGpu?oldGpu:new(editing.GpuController,editing.GpuHardware,editing.GpuDeviceId);if(!gpus.Contains(chosen))gpus.Add(chosen);GpuChoice.ItemsSource=gpus;GpuChoice.SelectedItem=chosen;
        SensorDetails.Text=string.Join("\n\n",list.Select(o=>$"{o.Reading!.Hardware} / {o.Reading.Name} : {o.Reading.Value}\n{o.Reading.Provider} · {o.Board}\nContrôleur : {o.Reading.ControllerId}\nParent : {o.Reading.ParentId} · {o.Reading.ParentName}\nCapteur : {o.Reading.Id}\\nIdentité physique : {o.Reading.DeviceId}"));
    }
    private static void FillTemperature(ComboBox box,SensorBinding? binding,List<SensorOption> list,bool preserve){var old=preserve?box.SelectedItem as SensorOption:null;var options=new List<SensorOption>{new(null,MissingLabel:"Automatique")};options.AddRange(list.Where(o=>o.Reading!.Type=="Temperature"));var target=old??(binding==null?null:new SensorOption(new(binding.SensorId,binding.Hardware,"",binding.SensorId,"Temperature",null,binding.ControllerId,Provider:binding.Provider),binding.Board,binding.Topology,"Capteur associé absent · "+binding.SensorId));if(target?.Reading!=null&&!options.Any(o=>o.Key==target.Key))options.Add(target);box.ItemsSource=options;box.SelectedItem=options.FirstOrDefault(o=>o.Key==target?.Key)??options[0];}
    private static SensorBinding? TemperatureBinding(ComboBox box)=>box.SelectedItem is SensorOption {Reading:{} r} o?new(r.Provider,o.Board,r.ControllerId,r.Id,r.Hardware,o.Topology):null;
    private void ReadAll()
    {
        ReadDisplay();editing.FanBindings=[];editing.FanLabels=[];foreach(var (role,control) in fanControls){editing.FanLabels[role]=string.IsNullOrWhiteSpace(control.Label.Text)?CoolingLabels.Label(role):control.Label.Text.Trim();if(control.Sensor.SelectedItem is SensorOption{Reading:{} r} o)editing.FanBindings.Add(new(role,r.Provider,o.Board,r.ControllerId,r.Id,control.Connector.Text.Trim(),"Association utilisateur",r.Hardware,o.Topology));}
        editing.CpuTemperature=TemperatureBinding(CpuTempChoice);editing.GpuTemperature=TemperatureBinding(GpuTempChoice);if(GpuChoice.SelectedItem is GpuOption gpu){editing.GpuController=gpu.Id;editing.GpuHardware=gpu.Hardware;editing.GpuDeviceId=gpu.DeviceId;}
        editing.Accent=(string?)Accent.SelectedValue??"#00E5FF";editing.Glow=Glow.Value;editing.Animation=Motion.Value;editing.Night=Night.IsChecked==true;editing.NightStart=Number(NightStart);editing.NightEnd=Number(NightEnd);editing.MinimizeToTray=Tray.IsChecked==true;editing.AutomaticFps=AutoFps.IsChecked==true;editing.NetworkAdapter=Network.Text.Trim();editing.Games=Lines(IncludedGames.Text);editing.IgnoredGames=Lines(IgnoredGames.Text);editing.Validate();
    }
    private static string[] Lines(string s)=>s.Split(['\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(Path.GetFileNameWithoutExtension).Where(s=>!string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()!;
    private async void Save(object sender,RoutedEventArgs e){try{ReadAll();if(previewOnly)ConfigStore.Save(editing,Path.Combine(ConfigStore.Root,"preview-settings.json"));else ConfigStore.Save(editing);running=ConfigStore.Clone(editing);if(previewOnly)running.AutomaticFps=false;hub.Update(running);foreach(string id in hosts.Keys.Except(running.Displays.Select(d=>d.InstanceId)).ToArray()){await hosts[id].DisposeAsync();hosts.Remove(id);frames.TryRemove(id,out _);}foreach(var d in running.Displays){if(hosts.TryGetValue(d.InstanceId,out var h))h.Update(running,d);else EnsureHost(d);}RefreshDisplayChoices();Status.Text=previewOnly?"Réglages d’aperçu enregistrés séparément. Aucun envoi USB.":"Réglages enregistrés et appliqués.";}catch(Exception ex){Status.Text=ex.Message;}}
    private void RefreshSensors(object sender,RoutedEventArgs e){FillSensors(true);Status.Text="Inventaire actualisé. Les associations existantes sont conservées.";}
    private async void StartSensors(object sender,RoutedEventArgs e)
    {
        if(previewOnly){Status.Text="Fermez l’aperçu et lancez normalement l’application pour activer ses capteurs.";return;}
        if(ConfigStore.IsInstalled){FillSensors(true);var data=HardwareSensors.Read();Status.Text=data!=null&&DateTimeOffset.UtcNow-data.Time<TimeSpan.FromSeconds(5)?"Collecte installée : "+data.Status:"Collecte indisponible. Relancez l’installateur pour réparer les composants, puis redémarrez Windows si demandé.";return;}
        try{
            if(!LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled){string installer=Path.Combine(AppContext.BaseDirectory,"tools","PawnIO","PawnIO_setup.exe");if(!File.Exists(installer))throw new FileNotFoundException("Composant PawnIO absent du dossier portable.");if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(installer)))!="1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032")throw new InvalidDataException("Composant PawnIO invalide.");using var setup=Process.Start(new ProcessStartInfo(installer){UseShellExecute=true,Verb="runas"});if(setup!=null)await setup.WaitForExitAsync();if(!LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled){Status.Text="PawnIO n’est pas installé. Les autres fonctions restent disponibles.";return;}}
            Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Lou.ControlCenter.exe"),"--sensor-collector --parent-pid "+Environment.ProcessId){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});editing.HardwareMonitoring=true;running.HardwareMonitoring=true;ConfigStore.Save(running);Status.Text="Autorisation demandée à Windows pour la lecture des capteurs.";await Task.Delay(4000);if(!disposed)FillSensors(true);
        }catch(Exception ex){Status.Text="Activation : "+ex.Message;}
    }
    private async void SearchCity(object sender,RoutedEventArgs e){searchStop.Cancel();searchStop.Dispose();searchStop=new();var token=searchStop.Token;try{CityStatus.Text="Recherche…";var results=await cities.SearchAsync(CityQuery.Text,token);if(token.IsCancellationRequested)return;CityResults.ItemsSource=results;CityResults.SelectedIndex=results.Length>0?0:-1;CityStatus.Text=results.Length>0?"Choisissez la ville et le pays correspondants.":"Aucun résultat. Essayez le nom complet de la ville.";}catch(OperationCanceledException){}catch(Exception ex){CityStatus.Text="Recherche indisponible : "+ex.Message;}}
    private void CityKey(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==Key.Enter)SearchCity(sender,new());}
    private void SelectCity(object sender,RoutedEventArgs e){if(CityResults.SelectedItem is not City city)return;editing.Location=city.Label;editing.Latitude=city.Latitude;editing.Longitude=city.Longitude;editing.CitySelected=true;CityStatus.Text="Ville choisie : "+city.Label+". Enregistrez pour l’appliquer.";}
    private void TestDisplay(object sender,RoutedEventArgs e){if(selected!=null&&hosts.TryGetValue(selected.InstanceId,out var h)){h.Calibration=!h.Calibration;Status.Text=h.Calibration?"Mire active. Réglez l’orientation et enregistrez, puis arrêtez la mire.":"Affichage normal rétabli.";}}
    private void ResetDisplay(object sender,RoutedEventArgs e){if(selected!=null){selected.Profile=null;Status.Text="Enregistrez pour relire le profil matériel de cet écran.";}}
    private void ChooseBackground(object sender,RoutedEventArgs e){try{var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.webp"};if(dialog.ShowDialog()!=true)return;var page=(PageId?)BackgroundPage.SelectedValue??PageId.System;using(var codec=SkiaSharp.SKCodec.Create(dialog.FileName)){if(codec==null||codec.Info.Width>8192||codec.Info.Height>8192)throw new InvalidDataException("Image invalide ou trop grande (8 192 pixels maximum).");}string folder=Path.Combine(ConfigStore.Root,"Backgrounds");Directory.CreateDirectory(folder);string name=page+"-"+Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName);File.Copy(dialog.FileName,Path.Combine(folder,name));editing.Backgrounds[page]=Path.Combine("Data","Backgrounds",name);BackgroundInfo.Text="Décor copié dans le dossier des données. Enregistrez pour l’appliquer.";}catch(Exception ex){Status.Text=ex.Message;}}
    private void ResetBackground(object sender,RoutedEventArgs e){editing.Backgrounds.Remove((PageId?)BackgroundPage.SelectedValue??PageId.System);BackgroundInfo.Text="Décor d’origine sélectionné. Enregistrez pour l’appliquer.";}
    private void ExportConfig(object sender,RoutedEventArgs e){try{ReadAll();var dialog=new SaveFileDialog{Filter="Configuration LOU|*.json",FileName="LOU-reglages.json"};if(dialog.ShowDialog()==true){ConfigStore.Save(editing,dialog.FileName);Status.Text="Configuration exportée. Les images personnalisées restent dans Data/Backgrounds.";}}catch(Exception ex){Status.Text=ex.Message;}}
    private void ImportConfig(object sender,RoutedEventArgs e){try{var dialog=new OpenFileDialog{Filter="Configuration LOU|*.json"};if(dialog.ShowDialog()==true){editing=ConfigStore.Load(dialog.FileName);selected=null;FillGlobal();FillSensors(false);RefreshDisplayChoices();Status.Text="Configuration chargée. Vérifiez les associations puis enregistrez pour l’appliquer.";}}catch(Exception ex){Status.Text=ex.Message;}}
    private void OpenData(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("explorer.exe",ConfigStore.Root){UseShellExecute=true});
    private void GoScreens(object sender,RoutedEventArgs e)=>Tabs.SelectedIndex=1;
    private void GoSensors(object sender,RoutedEventArgs e)=>Tabs.SelectedIndex=2;
    private void GoWeather(object sender,RoutedEventArgs e)=>Tabs.SelectedIndex=3;
    private void ScrollPages(object sender,MouseWheelEventArgs e){e.Handled=true;var parent=System.Windows.Media.VisualTreeHelper.GetParent(Pages);while(parent!=null&&parent is not ScrollViewer)parent=System.Windows.Media.VisualTreeHelper.GetParent(parent);if(parent is ScrollViewer scroll)scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta);}
    private void PowerChanged(object sender,PowerModeChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{foreach(var h in hosts.Values)if(e.Mode==PowerModes.Suspend)h.Suspend();else if(e.Mode==PowerModes.Resume)h.Resume();});
    private async void Quit(object sender,RoutedEventArgs e){if(closing)return;closing=true;await ShutdownHost();Close();System.Windows.Application.Current.Shutdown();}
    public async Task ShutdownHost(){if(disposed)return;disposed=true;timer.Stop();SystemEvents.PowerModeChanged-=PowerChanged;searchStop.Cancel();tray.Visible=false;tray.Dispose();foreach(var h in hosts.Values)await h.DisposeAsync();await hub.DisposeAsync();cities.Dispose();searchStop.Dispose();}
    private void Capture(string path){UpdateLayout();var image=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(this);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);png.Save(file);}
    public async Task CaptureReview(){
        string folder=Path.Combine(ConfigStore.IsInstalled?ConfigStore.Root:AppContext.BaseDirectory,"Review");Directory.CreateDirectory(folder);
        for(int i=0;i<Tabs.Items.Count;i++){Tabs.SelectedIndex=i;await Task.Delay(350);Capture(Path.Combine(folder,$"ui-{i}.png"));}
        if(previewOnly&&editing.Displays.Count>1){
            Tabs.SelectedIndex=1;DisplayChoice.SelectedIndex=0;Interval.Text="27";Save(this,new());var saved=ConfigStore.Load(Path.Combine(ConfigStore.Root,"preview-settings.json"));if(saved.Displays[0].PageIntervalSeconds!=27||saved.Displays[1].PageIntervalSeconds!=15)throw new InvalidOperationException("L’isolation du délai par écran a échoué.");
            DisplayChoice.SelectedIndex=1;if(Interval.Text!="15")throw new InvalidOperationException("La sélection de l’écran perd ses réglages.");
            Tabs.SelectedIndex=0;PreviewChoice.SelectedIndex=1;await Task.Delay(500);Capture(Path.Combine(folder,"ui-square.png"));
        }
        Width=1120;Height=740;await Task.Delay(350);Capture(Path.Combine(folder,"ui-compact.png"));
        File.WriteAllText(Path.Combine(folder,"ui-verification.json"),JsonSerializer.Serialize(new{Time=DateTimeOffset.Now,PreviewOnly=previewOnly,Displays=editing.Displays.Count,IndependentDelay=true,UsbFrames=hosts.Values.Sum(h=>h.Frames)},ConfigStore.Json));
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



