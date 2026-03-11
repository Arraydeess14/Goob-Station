using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Balloons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BloonTrackEndComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Lives = 100;

    [DataField, AutoNetworkedField]
    public int MaxLives = 100;

    [DataField, AutoNetworkedField]
    public int Cash = 0;

    [DataField, AutoNetworkedField]
    public string? LinkId;

    [DataField]
    public bool LossLocked = false;

    [DataField]
    public float LossResetTimer = 0f;

    [DataField]
    public float LossResetDelay = 1f;

    public EntityUid? LinkedSpawner;
}
