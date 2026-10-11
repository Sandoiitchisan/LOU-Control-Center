using LibreHardwareMonitor.Hardware;
namespace Lou.Providers;
// Never load control settings from another application or an imported JSON file.
// LHM's Undefined control mode leaves firmware/driver fan ownership untouched.
public sealed class ReadOnlyHardwareSettings:ISettings
{
    public bool Contains(string name)=>false;
    public string GetValue(string name,string value)=>name.EndsWith("/control/mode",StringComparison.Ordinal)?((int)ControlMode.Undefined).ToString(System.Globalization.CultureInfo.InvariantCulture):value;
    public void SetValue(string name,string value){}
    public void Remove(string name){}
}
