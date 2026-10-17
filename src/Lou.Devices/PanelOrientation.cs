namespace Lou.Devices;

// Initial physical orientation only. The user's saved orientation always takes precedence.
internal static class PanelOrientation
{
    public static int Jpeg(int width,int height,int pm,int sub)=>(width,height) switch {
        (480,480) when pm==6=>180,
        (1600,720)=>sub==3?0:180,
        (1920,462) or (1920,440)=>sub is 2 or 3 or 4?0:180,
        (1280,480)=>sub==2?90:0,
        (960,320)=>sub<5?0:180,
        (960,540)=>sub is 5 or 7?180:0,
        _=>0
    };
}
