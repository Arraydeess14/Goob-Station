using Content.Goobstation.Shared.Balloons;
using Content.Server.Popups;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Goobstation.Server.Balloons;

public sealed class BalloonSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BalloonSpawnerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BalloonSpawnerComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<BalloonSpawnerComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<BalloonSpawnerComponent, AnchorStateChangedEvent>(OnSpawnerAnchorChanged);
    }

    private void OnNewLink(Entity<BalloonSpawnerComponent> ent, ref NewLinkEvent args)
    {
        if (!TryComp<BloonTrackEndComponent>(args.Source, out var trackEnd))
            return;

        ent.Comp.LinkedTrackEnd = args.Source;
        trackEnd.LinkedSpawner = ent.Owner;

        Dirty(ent);
        Dirty(args.Source, trackEnd);
    }

    private void OnPortDisconnected(Entity<BalloonSpawnerComponent> ent, ref PortDisconnectedEvent args)
    {
        if (ent.Comp.LinkedTrackEnd != args.RemovedPortUid)
            return;

        if (TryComp<BloonTrackEndComponent>(args.RemovedPortUid, out var trackEnd) &&
            trackEnd.LinkedSpawner == ent.Owner)
        {
            trackEnd.LinkedSpawner = null;
            Dirty(args.RemovedPortUid, trackEnd);
        }

        ent.Comp.LinkedTrackEnd = null;
        Dirty(ent);
    }

    private void OnInteractHand(Entity<BalloonSpawnerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!Transform(ent).Anchored)
        {
            _popup.PopupEntity("The spawner must be anchored.", ent, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.LinkedTrackEnd is not { } trackEndUid || !TryComp<TransformComponent>(trackEndUid, out var trunkXform) || !trunkXform.Anchored)
        {
            _popup.PopupEntity("The bloon trunk must be anchored.", ent, args.User);
            args.Handled = true;
            return;
        }

        if (!HasValidTrackEnd(ent))
        {
            _popup.PopupEntity("This spawner is not linked to a bloon track end.", ent, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.Active)
        {
            _popup.PopupEntity("This spawner is already active.", ent, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.CurrentRound >= ent.Comp.Rounds.Count)
        {
            ResetSpawner(ent.Owner, ent.Comp);
            _popup.PopupEntity("All rounds completed. Spawner reset to round 1.", ent, args.User);
            args.Handled = true;
            return;
        }

        if (!ent.Comp.TrackValidated)
        {
            StartValidation(ent, args.User);
            args.Handled = true;
            return;
        }

        StartRound(ent, args.User);
        args.Handled = true;
    }

    private void StartRound(Entity<BalloonSpawnerComponent> ent, EntityUid user)
    {
        if (!HasValidTrackEnd(ent))
        {
            _popup.PopupEntity("This spawner is not linked to a bloon track end.", ent, user);
            return;
        }

        var roundId = ent.Comp.Rounds[ent.Comp.CurrentRound];
        var round = _prototype.Index(roundId);

        ent.Comp.Pending.Clear();

        foreach (var entry in round.Entries)
        {
            for (var i = 0; i < entry.Amount; i++)
            {
                ent.Comp.Pending.Enqueue(new QueuedBalloonSpawn
                {
                    Balloon = entry.Balloon,
                    Delay = entry.Delay
                });
            }
        }

        ent.Comp.Active = true;
        ent.Comp.NextSpawnTimer = 0f;
        Dirty(ent);

        _popup.PopupEntity($"Starting round {ent.Comp.CurrentRound + 1}.", ent, user);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BalloonSpawnerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var spawner, out var xform))
        {
            if (!spawner.Active)
                continue;

            if (!HasValidTrackEnd((uid, spawner)))
            {
                spawner.Active = false;
                spawner.Pending.Clear();
                spawner.NextSpawnTimer = 0f;
                Dirty(uid, spawner);

                Log.Warning($"Bloon spawner {ToPrettyString(uid)} lost its linked track end while active.");
                continue;
            }

            if (spawner.Pending.Count == 0)
            {
                spawner.Active = false;
                spawner.CurrentRound++;
                spawner.NextSpawnTimer = 0f;
                Dirty(uid, spawner);
                continue;
            }

            spawner.NextSpawnTimer -= frameTime;
            if (spawner.NextSpawnTimer > 0f)
                continue;

            var next = spawner.Pending.Dequeue();
            var spawned = Spawn(next.Balloon, xform.Coordinates);

            if (TryComp<BalloonComponent>(spawned, out var balloon))
            {
                balloon.LinkedTrackEnd = spawner.LinkedTrackEnd;

                if (TryFindFirstTrackPiece(uid, xform, out var firstTrack, out var dir))
                {
                    balloon.CurrentTrackPiece = firstTrack;
                    balloon.TravelDirection = dir;
                    balloon.MoveTarget = Transform(firstTrack).Coordinates;
                }
            }

            spawner.NextSpawnTimer = next.Delay;
            Dirty(uid, spawner);
        }
    }

    public void ResetSpawner(EntityUid uid, BalloonSpawnerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.CurrentRound = 0;
        comp.Active = false;
        comp.Pending.Clear();
        comp.NextSpawnTimer = 0f;

        Dirty(uid, comp);
    }

    private bool HasValidTrackEnd(Entity<BalloonSpawnerComponent> ent)
    {
        return ent.Comp.LinkedTrackEnd is { } trackEnd &&
               !Deleted(trackEnd) &&
               HasComp<BloonTrackEndComponent>(trackEnd) &&
               TryComp<TransformComponent>(trackEnd, out var xform) &&
               xform.Anchored;
    }

    public void StopSpawner(EntityUid uid, BalloonSpawnerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Active = false;
        comp.Pending.Clear();
        comp.NextSpawnTimer = 0f;

        Dirty(uid, comp);
    }

    private bool TryFindFirstTrackPiece(EntityUid spawnerUid, TransformComponent xform, out EntityUid trackUid, out Direction direction)
    {
        var checks = new (Vector2 Offset, Direction Dir)[]
        {
        (new Vector2(1f, 0f), Direction.East),
        (new Vector2(-1f, 0f), Direction.West),
        (new Vector2(0f, 1f), Direction.North),
        (new Vector2(0f, -1f), Direction.South),
        };

        foreach (var check in checks)
        {
            var coords = xform.Coordinates.Offset(check.Offset);
            var ents = _lookup.GetEntitiesInRange(coords, 0.2f);

            foreach (var ent in ents)
            {
                if (!HasComp<BloonTrackPieceComponent>(ent))
                    continue;

                trackUid = ent;
                direction = check.Dir;
                return true;
            }
        }

        trackUid = default;
        direction = default;
        return false;
    }
    // Track Validation
    public void CompleteValidation(EntityUid spawnerUid, HashSet<EntityUid> pieces, BalloonSpawnerComponent? comp = null)
    {
        if (!Resolve(spawnerUid, ref comp))
            return;

        if (!comp.ValidationInProgress)
            return;

        comp.TrackValidated = true;
        comp.ValidationInProgress = false;
        comp.ValidatedTrackPieces.Clear();

        foreach (var piece in pieces)
        {
            comp.ValidatedTrackPieces.Add(piece);

            if (TryComp<BloonTrackPieceComponent>(piece, out var trackPiece))
            {
                trackPiece.Validated = true;
                Dirty(piece, trackPiece);
            }
        }

        Dirty(spawnerUid, comp);
        _popup.PopupEntity("Track validated.", spawnerUid);
    }
    private void StartValidation(Entity<BalloonSpawnerComponent> ent, EntityUid user)
    {
        if (ent.Comp.ValidationInProgress)
        {
            _popup.PopupEntity("Track validation already in progress.", ent, user);
            return;
        }

        if (!HasValidTrackEnd(ent))
        {
            _popup.PopupEntity("This spawner is not linked to a bloon track end.", ent, user);
            return;
        }

        ent.Comp.ValidationInProgress = true;
        ent.Comp.TrackValidated = false;
        ent.Comp.ValidatedTrackPieces.Clear();
        Dirty(ent);

        var spawned = Spawn("ValidatorBalloon", Transform(ent).Coordinates);

        if (TryComp<BalloonComponent>(spawned, out var balloon))
        {
            balloon.IsValidator = true;
            balloon.ValidatorSpawner = ent.Owner;
            balloon.LinkedTrackEnd = ent.Comp.LinkedTrackEnd;
            balloon.ValidatorLifetime = 5f;
            balloon.ValidatorStuckTimer = 0f;
            balloon.LastValidatorPosition = Transform(spawned).Coordinates.Position;

            if (TryFindFirstTrackPiece(ent.Owner, Transform(ent), out var firstTrack, out var dir))
            {
                balloon.CurrentTrackPiece = firstTrack;
                balloon.TravelDirection = dir;
                balloon.MoveTarget = Transform(firstTrack).Coordinates;
                balloon.ValidatedPieces.Add(firstTrack);
                balloon.ValidatedVisits.Add((firstTrack, dir));
            }
            else
            {
                FailValidation(ent.Owner, ent.Comp);
                QueueDel(spawned);
                _popup.PopupEntity("Track validation failed.", ent, user);
                return;
            }
        }

        _popup.PopupEntity("Validating track...", ent, user);
    }
    public void InvalidateTrack(EntityUid spawnerUid, BalloonSpawnerComponent? comp = null)
    {
        if (!Resolve(spawnerUid, ref comp))
            return;

        foreach (var piece in comp.ValidatedTrackPieces)
        {
            if (TryComp<BloonTrackPieceComponent>(piece, out var trackPiece))
            {
                trackPiece.Validated = false;
                Dirty(piece, trackPiece);
            }
        }

        ClearLinkedBloons(comp.LinkedTrackEnd);

        if (comp.LinkedTrackEnd is { } trackEndUid &&
            TryComp<BloonTrackEndComponent>(trackEndUid, out var trackEnd))
        {
            trackEnd.Lives = trackEnd.MaxLives;
            trackEnd.Cash = 0;
            trackEnd.LossLocked = false;
            trackEnd.LossResetTimer = 0f;
            Dirty(trackEndUid, trackEnd);
        }

        comp.TrackValidated = false;
        comp.ValidationInProgress = false;
        comp.ValidatedTrackPieces.Clear();

        ResetSpawner(spawnerUid, comp);
        Dirty(spawnerUid, comp);

        _popup.PopupEntity("Track broken Dickhead!!", spawnerUid);
    }

    public void FailValidation(EntityUid spawnerUid, BalloonSpawnerComponent? comp = null)
    {
        if (!Resolve(spawnerUid, ref comp))
            return;

        if (!comp.ValidationInProgress)
            return;

        comp.TrackValidated = false;
        comp.ValidationInProgress = false;
        comp.ValidatedTrackPieces.Clear();

        Dirty(spawnerUid, comp);
        _popup.PopupEntity("Track validation failed.", spawnerUid);
    }
    private void ClearLinkedBloons(EntityUid? trackEndUid)
    {
        if (trackEndUid == null)
            return;

        var query = EntityQueryEnumerator<BalloonComponent>();
        while (query.MoveNext(out var uid, out var balloon))
        {
            if (balloon.LinkedTrackEnd != trackEndUid)
                continue;

            balloon.ProcessedPop = true;
            QueueDel(uid);
        }
    }

    private void OnSpawnerAnchorChanged(Entity<BalloonSpawnerComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        InvalidateTrack(ent.Owner, ent.Comp);
    }
}
