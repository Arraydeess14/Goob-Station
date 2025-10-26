namespace Content.Goobstation.Shared.Bloons;

[RegisterComponent]
public sealed partial class BloonSpawnerComponent : Component
{
    [DataField("bloonId", required: true)]
    public string[] BloonID = new string[10];

    [DataField("delay", required: true)]
    public int Delay = 500;

};
