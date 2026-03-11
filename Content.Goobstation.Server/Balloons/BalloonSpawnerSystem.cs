using Content.Goobstation.Shared.Balloons;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Interaction;
using Content.Server.Popups;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.Balloons;

public sealed class BalloonSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BalloonSpawnerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BalloonSpawnerComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<BalloonSpawnerComponent, PortDisconnectedEvent>(OnPortDisconnected);
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
                Dirty(uid, spawner);
                continue;
            }

            spawner.NextSpawnTimer -= frameTime;
            if (spawner.NextSpawnTimer > 0f)
                continue;

            var next = spawner.Pending.Dequeue();
            var spawned = Spawn(next.Balloon, xform.Coordinates);

            if (TryComp<BalloonComponent>(spawned, out var balloon))
                balloon.LinkedTrackEnd = spawner.LinkedTrackEnd;

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
               HasComp<BloonTrackEndComponent>(trackEnd);
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
}
