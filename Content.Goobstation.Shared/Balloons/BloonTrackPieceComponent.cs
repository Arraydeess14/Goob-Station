using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Balloons;

public enum BloonTrackPieceType : byte
{
    Straight,
    LeftTurn,
    RightTurn,
    Cross
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BloonTrackPieceComponent : Component
{
    [DataField, AutoNetworkedField]
    public BloonTrackPieceType PieceType = BloonTrackPieceType.Straight;

    [DataField, AutoNetworkedField]
    public bool Validated = false;

    [DataField, AutoNetworkedField]
    public bool AnchoredVisual = false;
}
