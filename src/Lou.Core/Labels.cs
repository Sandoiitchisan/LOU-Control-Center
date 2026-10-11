namespace Lou.Core;
public sealed record Choice<T>(T Value,string Label);
public static class Labels
{
    public static string Page(PageId id)=>id switch{PageId.System=>"Système",PageId.Weather=>"Météo",PageId.Music=>"Musique",PageId.Gaming=>"Jeux",_=>"Ambiance"};
    public static Choice<PageId>[] Pages=>Enum.GetValues<PageId>().Select(p=>new Choice<PageId>(p,Page(p))).ToArray();
    public static Choice<RotationMode>[] Modes=>[new(RotationMode.Automatic,"Automatique"),new(RotationMode.Smart,"Intelligente"),new(RotationMode.Fixed,"Page fixe")];
    public static Choice<Transition>[] Transitions=>[new(Transition.Slide,"Glissement"),new(Transition.Glitch,"Distorsion"),new(Transition.Scanline,"Balayage"),new(Transition.None,"Aucune")];
}

