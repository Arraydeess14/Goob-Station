using Content.Goobstation.Shared.Balloons;
using Content.Server.Stack;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Goobstation.Server.Balloons;

public sealed class BalloonSystem : EntitySystem
{
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BloonTrackEndSystem _trackEnd = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private sealed record PendingChildSpawn(
        EntProtoId Proto,
        MapCoordinates Coords,
        EntityUid? LinkedTrackEnd,
        EntityUid? CurrentTrackPiece,
        Direction? TravelDirection,
        EntityCoordinates? MoveTarget,
        EntityUid? CurrentTrackEndTarget,
        int SpilloverDamage,
        bool IsRegrow,
        EntProtoId? RegrowCap);

    private sealed record PendingRegrow(
    EntityUid OldUid,
    EntProtoId NewProto,
    EntityCoordinates Coords,
    EntityUid? LinkedTrackEnd,
    EntityUid? CurrentTrackPiece,
    Direction? TravelDirection,
    EntityCoordinates? MoveTarget,
    EntityUid? CurrentTrackEndTarget,
    bool IsRegrow,
    EntProtoId? RegrowCap);

    private readonly List<PendingChildSpawn> _pendingChildSpawns = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BalloonComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<BalloonComponent, DestructionEventArgs>(OnBalloonDestroyed);
    }

    public override void Update(float frameTime)
    {
        if (_pendingChildSpawns.Count > 0)
        {
            var pendingChildren = new List<PendingChildSpawn>(_pendingChildSpawns);
            _pendingChildSpawns.Clear();

            foreach (var spawn in pendingChildren)
            {
                var spawned = Spawn(spawn.Proto, spawn.Coords);

                if (!TryComp<BalloonComponent>(spawned, out var child))
                    continue;

                child.LinkedTrackEnd = spawn.LinkedTrackEnd;
                child.CurrentTrackPiece = spawn.CurrentTrackPiece;
                child.TravelDirection = spawn.TravelDirection;
                child.MoveTarget = spawn.MoveTarget;
                child.CurrentTrackEndTarget = spawn.CurrentTrackEndTarget;
                child.IsRegrow = spawn.IsRegrow;
                child.RegrowCap = spawn.RegrowCap;

                if (child.IsRegrow)
                    child.RegrowTimer = child.RegrowDelay;
                Dirty(spawned, child);
                if (spawn.SpilloverDamage > 0)
                    ApplyDamage((spawned, child), spawn.SpilloverDamage);
            }
        }

        var pendingRegrows = new List<PendingRegrow>();

        var query = EntityQueryEnumerator<BalloonComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var balloon, out var xform))
        {
            if (!balloon.IsRegrow)
                continue;

            if (balloon.ProcessedPop)
                continue;
            Logger.Info($"Regrow check {ToPrettyString(uid)} isRegrow={balloon.IsRegrow} timer={balloon.RegrowTimer} regrowInto={balloon.RegrowInto} cap={balloon.RegrowCap}");
            if (balloon.RegrowInto == null || balloon.RegrowCap == null)
                continue;

            var currentProto = MetaData(uid).EntityPrototype?.ID;
            if (currentProto == balloon.RegrowCap)
                continue;

            balloon.RegrowTimer -= frameTime;
            if (balloon.RegrowTimer > 0f)
                continue;

            pendingRegrows.Add(new PendingRegrow(
                uid,
                balloon.RegrowInto.Value,
                xform.Coordinates,
                balloon.LinkedTrackEnd,
                balloon.CurrentTrackPiece,
                balloon.TravelDirection,
                balloon.MoveTarget,
                balloon.CurrentTrackEndTarget,
                true,
                balloon.RegrowCap));
        }

        foreach (var regrow in pendingRegrows)
        {
            if (Deleted(regrow.OldUid))
                continue;

            var regrown = Spawn(regrow.NewProto, regrow.Coords);

            if (TryComp<BalloonComponent>(regrown, out var regrowComp))
            {
                regrowComp.LinkedTrackEnd = regrow.LinkedTrackEnd;
                regrowComp.CurrentTrackPiece = regrow.CurrentTrackPiece;
                regrowComp.TravelDirection = regrow.TravelDirection;
                regrowComp.MoveTarget = regrow.MoveTarget;
                regrowComp.CurrentTrackEndTarget = regrow.CurrentTrackEndTarget;
                regrowComp.IsRegrow = regrow.IsRegrow;
                regrowComp.RegrowTimer = regrowComp.RegrowDelay;
                regrowComp.RegrowCap = regrow.RegrowCap;

                Dirty(regrown, regrowComp);
            }

            if (TryComp<BalloonComponent>(regrow.OldUid, out var oldBalloon))
                oldBalloon.ProcessedPop = true;

            QueueDel(regrow.OldUid);
        }
    }

    private void OnBeforeDamageChanged(Entity<BalloonComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.ProcessedPop)
            return;

        var damage = GetBalloonDamage(ent, args.Damage);
        if (damage <= 0)
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        ApplyDamage(ent, damage);

        // Let normal hit flow continue, but prevent default damage from also applying.
        args.Damage = new DamageSpecifier();
    }

    private void OnBalloonDestroyed(Entity<BalloonComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.ProcessedPop)
            return;

        ent.Comp.ProcessedPop = true;
        HandleCashReward(ent);
        HandleChildSpawns(ent, 0);
    }

    public void ApplyDamage(Entity<BalloonComponent> ent, int damage)
    {
        if (damage <= 0 || Deleted(ent) || ent.Comp.ProcessedPop)
            return;

        if (ent.Comp.IsRegrow)
            ent.Comp.RegrowTimer = ent.Comp.RegrowDelay;

        if (damage < ent.Comp.CurrentPopHealth)
        {
            ent.Comp.CurrentPopHealth -= damage;
            Dirty(ent);
            return;
        }

        var leftoverDamage = damage - ent.Comp.CurrentPopHealth;
        PopBloon(ent, leftoverDamage);
    }

    private void PopBloon(Entity<BalloonComponent> ent, int leftoverDamage)
    {
        if (ent.Comp.ProcessedPop)
            return;

        ent.Comp.ProcessedPop = true;

        _audio.PlayPvs(
            new SoundPathSpecifier("/Audio/_Goobstation/BalloonEffect/balloon_pop.ogg"),
            ent.Owner,
            AudioParams.Default.WithVariation(0.125f).WithVolume(-4f));

        HandleCashReward(ent);
        HandleChildSpawns(ent, leftoverDamage);

        QueueDel(ent);
    }

    private int GetBalloonDamage(Entity<BalloonComponent> ent, DamageSpecifier damage)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return (int) MathF.Floor((float) damage.GetTotal());

        if (!_prototype.TryIndex<DamageContainerPrototype>(damageable.DamageContainerID, out var container))
            return (int) MathF.Floor((float) damage.GetTotal());

        var total = 0f;

        foreach (var (damageType, value) in damage.DamageDict)
        {
            if (value <= 0)
                continue;

            if (!container.SupportedTypes.Contains(damageType))
                continue;

            total += (float) value;
        }

        return (int) MathF.Floor(total);
    }

    private void HandleCashReward(Entity<BalloonComponent> ent)
    {
        var amount = _random.Next(ent.Comp.MinCash, ent.Comp.MaxCash + 1);
        if (amount <= 0)
            return;

        if (ent.Comp.LinkedTrackEnd is { } trackEndUid &&
            !Deleted(trackEndUid) &&
            TryComp<BloonTrackEndComponent>(trackEndUid, out _))
        {
            _trackEnd.AddCash(trackEndUid, amount);
            return;
        }

        SpawnOrMergeCash(ent, amount);
    }

    private void HandleChildSpawns(Entity<BalloonComponent> ent, int spilloverDamage)
    {
        if (ent.Comp.SpawnOnPop.Count == 0)
            return;

        var xform = Transform(ent);
        var parentMapPos = xform.WorldPosition;

        var backward = ent.Comp.TravelDirection switch
        {
            Direction.East => new Vector2(-1f, 0f),
            Direction.West => new Vector2(1f, 0f),
            Direction.North => new Vector2(0f, -1f),
            Direction.South => new Vector2(0f, 1f),
            _ => Vector2.Zero
        };

        const float spacing = 0.30f;

        for (var i = 0; i < ent.Comp.SpawnOnPop.Count; i++)
        {
            var proto = ent.Comp.SpawnOnPop[i];

            // First child at parent position; rest trail behind in single file.
            var offsetPos = parentMapPos + backward * (spacing * i);
            var spawnCoords = new MapCoordinates(offsetPos, xform.MapID);

            _pendingChildSpawns.Add(new PendingChildSpawn(
             proto,
             spawnCoords,
             ent.Comp.LinkedTrackEnd,
             ent.Comp.CurrentTrackPiece,
             ent.Comp.TravelDirection,
             ent.Comp.MoveTarget,
             ent.Comp.CurrentTrackEndTarget,
             spilloverDamage,
             ent.Comp.IsRegrow,
             ent.Comp.RegrowCap));
        }
    }

    private void SpawnOrMergeCash(Entity<BalloonComponent> ent, int amount)
    {
        var coords = Transform(ent).Coordinates;

        EntityUid? foundCash = null;

        foreach (var uid in _lookup.GetEntitiesInRange(coords, ent.Comp.MergeRange))
        {
            if (uid == ent.Owner)
                continue;

            if (!TryComp<StackComponent>(uid, out var stack))
                continue;

            if (stack.StackTypeId != ent.Comp.CashStackType)
                continue;

            foundCash = uid;
            break;
        }

        if (foundCash is { } cashUid)
        {
            if (TryComp<StackComponent>(cashUid, out var stack))
                _stack.SetCount(cashUid, stack.Count + amount, stack);

            return;
        }

        var spawned = Spawn(ent.Comp.CashPrototype, coords);

        if (TryComp<StackComponent>(spawned, out var newStack))
            _stack.SetCount(spawned, amount, newStack);
    }
}
