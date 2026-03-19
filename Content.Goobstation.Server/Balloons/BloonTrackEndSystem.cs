using Content.Goobstation.Shared.Balloons;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Physics.Events;

namespace Content.Goobstation.Server.Balloons;

public sealed class BloonTrackEndSystem : EntitySystem
{
    [Dependency] private readonly BalloonSpawnerSystem _spawner = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private const string CashPrototype = "SpaceCash";
    public override void Initialize()
    {
        SubscribeLocalEvent<BloonTrackEndComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<BloonTrackEndComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BloonTrackEndComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<BloonTrackEndComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BloonTrackEndComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<BloonTrackEndComponent, AnchorStateChangedEvent>(OnTrackEndAnchorChanged);
    }
    // Get cash
    private void OnInteractHand(Entity<BloonTrackEndComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Cash <= 0)
        {
            _popup.PopupEntity(Loc.GetString("bloon-track-end-empty"), ent, args.User);
            args.Handled = true;
            return;
        }

        var spawned = Spawn(CashPrototype, Transform(ent).Coordinates);

        if (!TryComp<StackComponent>(spawned, out var stack))
            return;

        _stack.SetCount(spawned, ent.Comp.Cash, stack);

        _popup.PopupEntity(
            Loc.GetString("bloon-track-end-withdrawn", ("cash", ent.Comp.Cash)),
            ent,
            args.User);

        ent.Comp.Cash = 0;
        Dirty(ent);
        args.Handled = true;
    }
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BloonTrackEndComponent>();

        while (query.MoveNext(out var uid, out var trackEnd))
        {
            if (!trackEnd.LossLocked)
                continue;

            trackEnd.LossResetTimer -= frameTime;
            if (trackEnd.LossResetTimer > 0f)
                continue;

            if (trackEnd.LinkedSpawner is { } spawnerUid &&
                !Deleted(spawnerUid) &&
                TryComp<BalloonSpawnerComponent>(spawnerUid, out var spawnerComp))
            {
                _spawner.ResetSpawner(spawnerUid, spawnerComp);
            }

            trackEnd.Lives = trackEnd.MaxLives;
            trackEnd.Cash = 0;
            trackEnd.LossLocked = false;
            trackEnd.LossResetTimer = 0f;
            Dirty(uid, trackEnd);
        }
    }
    private void OnNewLink(Entity<BloonTrackEndComponent> ent, ref NewLinkEvent args)
    {
        if (!TryComp<BalloonSpawnerComponent>(args.Sink, out var spawner))
            return;

        ent.Comp.LinkedSpawner = args.Sink;
        spawner.LinkedTrackEnd = ent.Owner;

        Dirty(ent);
        Dirty(args.Sink, spawner);
    }

    private void OnPortDisconnected(Entity<BloonTrackEndComponent> ent, ref PortDisconnectedEvent args)
    {
        if (ent.Comp.LinkedSpawner != args.RemovedPortUid)
            return;

        if (TryComp<BalloonSpawnerComponent>(args.RemovedPortUid, out var spawner) &&
            spawner.LinkedTrackEnd == ent.Owner)
        {
            spawner.LinkedTrackEnd = null;
            Dirty(args.RemovedPortUid, spawner);
        }

        ent.Comp.LinkedSpawner = null;
        Dirty(ent);
    }

    private void OnStartCollide(Entity<BloonTrackEndComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.LossLocked)
            return;

        if (!TryComp<BalloonComponent>(args.OtherEntity, out var balloon))
            return;

        // Validators are handled only by the movement system when they reach the trunk.
        if (balloon.IsValidator)
            return;

        if (balloon.ProcessedPop)
            return;

        balloon.ProcessedPop = true;
        TakeLives(ent.Owner, balloon.Lives, ent.Comp);
        QueueDel(args.OtherEntity);
    }

    public void AddCash(EntityUid uid, int amount, BloonTrackEndComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Cash += amount;
        Dirty(uid, comp);
    }

    public void TakeLives(EntityUid uid, int amount, BloonTrackEndComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (comp.LossLocked)
            return;

        comp.Lives = Math.Max(0, comp.Lives - amount);
        Dirty(uid, comp);

        if (comp.Lives > 0)
            return;

        comp.LossLocked = true;
        comp.LossResetTimer = comp.LossResetDelay;
        Dirty(uid, comp);

        if (comp.LinkedSpawner is { } spawnerUid &&
            !Deleted(spawnerUid) &&
            TryComp<BalloonSpawnerComponent>(spawnerUid, out var spawnerComp))
        {
            _spawner.StopSpawner(spawnerUid, spawnerComp);
        }

        ClearLinkedBloons(uid);
        _popup.PopupEntity(
          Loc.GetString("bloon-game-over"), uid, PopupType.Large);
    }
    // Clear spill over bloons from previous round
    private void ClearLinkedBloons(EntityUid trackEndUid)
    {
        var query = EntityQueryEnumerator<BalloonComponent>();
        while (query.MoveNext(out var uid, out var balloon))
        {
            if (balloon.LinkedTrackEnd != trackEndUid)
                continue;

            balloon.ProcessedPop = true;
            QueueDel(uid);
        }
    }

    private void OnExamined(Entity<BloonTrackEndComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("bloon-track-end-lives", ("current", ent.Comp.Lives), ("max", ent.Comp.MaxLives)));
        args.PushMarkup(Loc.GetString("bloon-track-end-cash", ("cash", ent.Comp.Cash)));
    }

    private void OnTrackEndAnchorChanged(Entity<BloonTrackEndComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        if (ent.Comp.LinkedSpawner is { } spawnerUid &&
            TryComp<BalloonSpawnerComponent>(spawnerUid, out var spawner))
        {
            _spawner.InvalidateTrack(spawnerUid, spawner);
        }
    }
}
