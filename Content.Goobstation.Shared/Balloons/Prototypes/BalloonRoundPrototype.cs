using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Balloons.Prototypes;

[Prototype("balloonRound")]
public sealed class BalloonRoundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<BalloonRoundEntry> Entries = new();
}

[DataDefinition]
public sealed partial class BalloonRoundEntry
{
    [DataField(required: true)]
    public EntProtoId Balloon = default!;

    [DataField]
    public int Amount = 1;

    [DataField]
    public float Delay = 0.5f;
}
