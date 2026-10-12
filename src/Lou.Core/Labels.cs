namespace Lou.Core;
public sealed record Choice<T>(T Value,string Label);
public static class Labels
{
    public static string Page(PageId id)=>id switch{PageId.System=>Localizer.T("Système"),PageId.Weather=>Localizer.T("Météo"),PageId.Music=>Localizer.T("Musique"),PageId.Gaming=>Localizer.T("Jeux"),_=>Localizer.T("Ambiance")};
    public static Choice<PageId>[] Pages=>Enum.GetValues<PageId>().Select(p=>new Choice<PageId>(p,Page(p))).ToArray();
    public static Choice<RotationMode>[] Modes=>[new(RotationMode.Automatic,Localizer.T("Automatique")),new(RotationMode.Smart,Localizer.T("Intelligente")),new(RotationMode.Fixed,Localizer.T("Page fixe"))];
    public static Choice<Transition>[] Transitions=>[new(Transition.Slide,Localizer.T("Glissement")),new(Transition.Glitch,Localizer.T("Distorsion")),new(Transition.Scanline,Localizer.T("Balayage")),new(Transition.None,Localizer.T("Aucune"))];
}

