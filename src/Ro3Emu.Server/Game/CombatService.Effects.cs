using System.Text.Json;
using Romsg;
using Ro3Emu.Server.Data;
using Ro3Emu.Server.Maps;

namespace Ro3Emu.Server.Game;

/// <summary>An owned skill NPC/trigger. NPCConfig identity and MG_Entity.owner survive AOI re-entry.</summary>
public sealed class SkillObject : WorldUnit
{
    public required GameSession OwnerSession { get; init; }
    public required long OwnerId { get; init; }
    public required int NpcId { get; init; }
    public required int EntityType { get; init; }
    public required ActiveSkillCast Cast { get; init; }
    public required long ExpiresMs { get; init; }
    public long NextTickMs { get; set; }
    public int IntervalMs { get; init; }
    public float Radius { get; init; }
    public bool Trap { get; init; }
    public bool Busy { get; set; }
    public bool Removing { get; set; }
    public int Charges { get; set; }
    public int ProtectionDistanceType { get; init; }
    public HashSet<int> Skills { get; } = new();

    public override EntityBase Entity() => new()
    {
        Uuid = (ulong)Uid, EntityTemplateID = (ulong)NpcId, EntityType = EntityType, Owner = OwnerId,
        AiMasterEntityId = (ulong)OwnerId, EntityCamp = 1, State = EntityState.Alive, BornTimeMs = Cast.StartMs,
        Face = Face, MovementInfo = new MovementInfo { Pos = Pos, Face = Face, ServerTime = MonsterService.NowMs },
        NotifyType = EntityBase.Types.NotifyType.All,
    };
}

public sealed partial class CombatService
{
    private long _skillObjectIds = 0x600000000000L;

    private async Task ResolveCastAsync(GameSession s, ActiveSkillCast action)
    {
        var row = action.Row;
        if (!CurrentCastMap(s, action)) return;
        foreach (var handler in CastLanded)
        {
            await handler(s, row);
            if (!CurrentCastMap(s, action)) return;
        }
        if (Script(row) == "Resurrection")
        {
            var target = FindPlayer(s, action.Request.TargetId, dead: true);
            if (target == null || target.SceneLoading || buffs.HasForbidden(target, 16)
                || s.WorldState.ResurrectionCastId != action.Id
                || Distance(s.Position, target.Position) > Range(row) + 1.5f) return;
            s.WorldState.ResurrectionCastId = 0;
            if (action.Request.SkillId == PublicSkill("WorldTreeSkillId"))
            {
                if (ReviveItemSkill != null && await ReviveItemSkill(s, target, row))
                    await buffs.NotifyWorldTreeReviveAsync(s, target.Role!.Name);
            }
            else if (ReviveOffer != null) await ReviveOffer(s, target, row);
            return;
        }
        if (await CreateSkillObjectAsync(s, action)) return;
        if (!CurrentCastMap(s, action)) return;
        // SkillConfig._bMovement is enum_SkillLockMoveType (may the caster walk while casting), not a displacement.
        await MoveCasterAsync(s, action);
        if (!CurrentCastMap(s, action)) return;
        await ApplyEffectsAsync(s, action, action.Request.TargetPos ?? action.Origin);
    }

    private List<MonsterUnit> SelectEnemies(GameSession s, ActiveSkillCast action, Vec3 center, float? radiusOverride = null)
    {
        var row = action.Row;
        int max = (int)Math.Max(1, GameData.Int(row, "_iTargetMax"));
        if (GameData.Int(row, "_iRangeType") != 2 && radiusOverride == null)
        {
            var target = FindMonster(s.WorldScene, action.Request.TargetId);
            return target != null && target.Hp > 0 && CanHitMonster(s, target) && Relation(row, false, race: target.Template.Race) ? [target] : [];
        }
        int release = (int)GameData.Int(row, "_iRelease");
        if (release is not (1 or 7 or 8) && radiusOverride == null) center = action.Origin;
        var range = Numbers(row, "_kRange");
        float radius = radiusOverride ?? (float)(range.FirstOrDefault(300) / 100);
        float height = GameData.Int(row, "_iAreaHeightRange") / 100f;
        bool InShape(MonsterUnit m)
        {
            if (height > 0 && MathF.Abs(m.Y - center.Y) > height) return false;
            float dx = m.X - center.X, dz = m.Z - center.Z;
            double radians = action.Request.Face * Math.PI / 180;
            double forward = dx * Math.Sin(radians) + dz * Math.Cos(radians), side = dx * Math.Cos(radians) - dz * Math.Sin(radians);
            if (radiusOverride == null && release is 3 or 5 or 6 or 7)
                return Math.Abs(side) <= radius / 2 && Math.Abs(forward) <= (range.Length > 1 ? range[1] / 200 : radius);
            if (Distance(center, m.Pos) > radius + 0.1f) return false;
            return radiusOverride != null || release != 4 || Math.Atan2(Math.Abs(side), forward) <= (range.Length > 1 ? range[1] : 90) * Math.PI / 360;
        }
        return world.Map(s.WorldScene).Units.OfType<MonsterUnit>().Where(m => m.Hp > 0 && CanHitMonster(s, m) && Relation(row, false, race: m.Template.Race) && InShape(m))
            .OrderBy(m => Distance(center, m.Pos)).ThenBy(m => m.Uid).Take(max).ToList();
    }

    private async Task ApplyEffectsAsync(GameSession s, ActiveSkillCast action, Vec3 center, SkillObject? field = null)
    {
        if (s.Dead || s.Role?.Id != action.OwnerId || s.WorldScene != action.WorldScene || s.SceneLoading
            || s.SceneGeneration != action.SceneGeneration || field?.Removing == true
            || !ReferenceEquals(s.Attachment, action.Attachment) || action.Attachment?.Cancelled == true) return;
        var row = action.Row;
        var castingRole = s.Role;
        bool CurrentCaster() => ReferenceEquals(s.Role, castingRole) && !s.SceneLoading
            && s.SceneGeneration == action.SceneGeneration && (field != null ? !field.Removing : !action.Cancelled)
            && s.WorldScene == action.WorldScene && sessions.All().Contains(s);
        bool CanApplyMoreEffects() => CurrentCaster() && ReferenceEquals(s.Attachment, action.Attachment)
            && action.Attachment?.Cancelled != true;
        // A published death still owes its callbacks after the cast/field is cancelled.
        // A cancelled but unchanged lease can finish committed consequences under its admitted owner.
        bool CurrentKillRecipient() => ReferenceEquals(s.Role, castingRole) && !s.SceneLoading
            && s.SceneGeneration == action.SceneGeneration && s.WorldScene == action.WorldScene
            && ReferenceEquals(s.Attachment, action.Attachment)
            && sessions.All().Contains(s);
        string script = Script(row);
        var targets = SelectEnemies(s, action, center, field?.Trap == true ? field.Radius : null);
        var playerTargets = GameData.Int(row, "_iRangeType") == 2 ? EventAreaTargets(s, row)
            : EventTarget(s, action.Request.TargetId) is { } eventTarget
                && PlayerDistance(s, eventTarget) <= Range(row) + 1.5f ? new[] { eventTarget } : [];
        if (playerTargets.Length > 0)
        {
            int maxTargets = checked((int)Math.Max(1, GameData.Int(row, "_iTargetMax")));
            var selected = targets.Select(m => (Id: m.Uid, Distance: MonsterService.Distance(m, s)))
                .Concat(playerTargets.Select(p => (Id: p.Role!.Id, Distance: PlayerDistance(s, p))))
                .OrderBy(p => p.Distance).ThenBy(p => p.Id).Take(maxTargets).Select(p => p.Id).ToHashSet();
            targets.RemoveAll(m => !selected.Contains(m.Uid));
            await HitEventPlayersAsync(s, playerTargets.Where(p => selected.Contains(p.Role!.Id)).ToArray(),
                row, action.Request.SkillId, HitEffect(GameData.Int(row, "_iScriptShow")), action.Id, CanApplyMoreEffects);
        }
        if (!CanApplyMoreEffects()) return; // player publication can yield before any monster hit is committed
        bool friendly = Relation(row, true, true) || Relation(row, true);
        var allies = new List<GameSession>();
        if (friendly)
        {
            if (GameData.Int(row, "_iRelease") == 1)
            {
                var ally = action.Request.TargetId == s.Role.Id ? s : FindPlayer(s, action.Request.TargetId);
                if (ally != null && Relation(row, true, ally == s)) allies.Add(ally);
            }
            else
            {
                float radius = field?.Radius ?? (float)(Numbers(row, "_kRange").FirstOrDefault(0) / 100);
                allies.AddRange(world.PlayersIn(s.WorldScene).Where(p => !p.Dead && Relation(row, true, p == s)
                    && (p == s || radius > 0 && Distance(p.Position, center) <= radius))
                    .OrderBy(p => Distance(p.Position, center)).Take((int)Math.Max(1, GameData.Int(row, "_iTargetMax"))));
                if (allies.Count == 0 && Relation(row, true, true)) allies.Add(s);
            }
        }
        var stats = character.Stats(s);
        var attacker = new Fighter(stats[103], stats[104], stats[109], stats[111], s.Progress!.Level, 1);
        var hit = new SkillHitInfo
        {
            SrcID = field?.Uid ?? action.SourceId, SkillId = action.Request.SkillId, SessionId = action.Id,
            RootCtxGuid = action.Id, ComboIndex = action.Request.ComboIndex, ShowPoint = HitEffect(GameData.Int(row, "_iScriptShow")),
            SrcElement = (int)Math.Max(1, GameData.Int(row, "_iEleType")),
        };
        var killed = new List<MonsterUnit>();
        var processed = new List<MonsterUnit>();
        bool damage = Numbers(row, "_kDamageParam1").Any(v => v > 0) || Numbers(row, "_kDamageParam2").Any(v => v > 0);
        try
        {
        foreach (var target in targets)
        {
            if (!CanApplyMoreEffects()) break; // a previous target's awaited callback may have invalidated the caster
            if (target.Dead || target.Hp <= 0) continue; // another awaited cast may already own this death source
            processed.Add(target);
            if (damage)
            {
                var defender = new Fighter(0, 0, target.Template.Flee, 0, target.Template.Level, target.Template.Element)
                { Def = target.Template.Def, Mdef = target.Template.Mdef, Flee = target.Template.Flee };
                var (amount, result) = Damage(attacker, defender, row, script == "Heal" ? 1 : 0);
                if (amount > 0)
                {
                    foreach (var modifier in DamageAdjusters) amount = Math.Max(0, modifier(s, target, row, result, amount));
                    foreach (var extra in ExtraHitDamage) amount += Math.Max(0, extra(s, target, row, amount));
                }
                long beforeHp = target.Hp;
                target.Hp = Math.Max(0, beforeHp - amount);
                long effective = beforeHp - target.Hp;
                if (target.Hp == 0) killed.Add(target); // reserve before awaited damage callbacks
                await SettleMonsterHitAsync(s, target, beforeHp, amount);
                if (CanApplyMoreEffects())
                    foreach (var handler in DealtAsync)
                    {
                        if (!CanApplyMoreEffects()) break;
                        await handler(s, target, row, amount);
                    }
                // The hit is already committed; an unchanged (even cancelled) lease
                // still owns its damage callback, while a replacement lease does not.
                if (effective > 0 && CurrentKillRecipient())
                    foreach (var handler in MonsterDamageApplied)
                    {
                        if (!CurrentKillRecipient()) break;
                        try { await handler(s, target, effective); }
                        catch (Exception ex) { log.LogError(ex, "Monster damage callback failed for {Uid}", target.Uid); }
                    }
                hit.Dsts.Add(new DamageInfo { DstID = target.Uid, Damage = amount, HitResult = result, DstElement = target.Template.Element, IsDead = target.Hp == 0 });
            }
            if (CanApplyMoreEffects())
                foreach (var tuple in Tuples(row, "_kBuff2"))
                    if (CanApplyMoreEffects() && RollHostileBuff(tuple, s, null, null, target, out int id, out int duration))
                        await buffs.ShowOnUnitAsync(target, id, duration, castingRole.Id);
            if (CanApplyMoreEffects() && script is ("Provoke" or "Taunt")) monsters.Engage(target, s);
            if (CanApplyMoreEffects() && !target.Dead && target.Hp > 0) await DisplaceTargetAsync(s, action, target);
        }
        // Buff1 is self for hostile selectors, but actual friend recipients for ally selectors (Detoxify, Blessing).
        foreach (var recipient in friendly ? allies : new List<GameSession> { s })
        {
            if (!CanApplyMoreEffects()) break;
            if (field == null)
                foreach (var tuple in Tuples(row, "_kBuff1"))
                    if (CanApplyMoreEffects() && RollBuff(tuple, out int id, out int duration)) await buffs.AddAsync(recipient, id, duration, castingRole.Id);
            if (friendly && damage)
            {
                double multiplier = Numbers(row, "_kDamageParam1").FirstOrDefault(), flat = Numbers(row, "_kDamageParam2").FirstOrDefault();
                // Heal's friendly branch is P1[0]/P2[0]; its enemy branch is [1] (description + select race mask).
                long amount = (long)Math.Max(0, Math.Round(stats[104] * multiplier / 10000 + flat));
                foreach (var modifier in ExtraHeal) amount = modifier(s, amount);
                var before = character.Stats(recipient);
                long actual = Math.Min(amount, Math.Max(0, before[101] - before[2000]));
                await character.SetHpSpAsync(recipient, before[2000] + amount, null);
                hit.Dsts.Add(new DamageInfo { DstID = recipient.Role!.Id, Damage = actual, DamageType = DamageType.Heal, DstElement = 1 });
                foreach (var handler in Healed) handler(s, actual);
                foreach (var handler in HealedAsync) await handler(s, actual);
            }
            if (!damage && (friendly || hit.Dsts.Count == 0))
            {
                hit.Dsts.Add(new DamageInfo { DstID = recipient.Role!.Id, Damage = 0, DamageType = DamageType.Damage, HitResult = DamageHitResult.Normal, DstElement = 1 });
            }
            if (script is "Detoxify" or "HealingSkills")
            {
                // (inferred) cleanse exact described groups rather than removing every beneficial/hostile status.
                int[] groups = script == "Detoxify" ? [9030] : [9023, 9025];
                foreach (var buff in recipient.Buffs.Where(b => data.Row("BuffConfig", b.ConfigId) is { } br && groups.Contains((int)GameData.Int(br, "_iBuffID"))).ToArray())
                    await buffs.RemoveAsync(recipient, buff);
            }
        }
        if (hit.Dsts.Count > 0)
        {
            foreach (var result in hit.Dsts)
            {
                if (!CurrentCaster()) break;
                await RecordDamageStatisticAsync(s, new DamageStatistics
                {
                    DamageSourceId = hit.SrcID, DamageTargetId = result.DstID, DamageValue = result.Damage,
                    DamageBeShield = result.SheildDamage, DamageType = result.DamageType, HitResult = result.HitResult, SkillId = hit.SkillId,
                });
            }
            if (CurrentCaster() && hit.Dsts.Any(d => d.HitResult != DamageHitResult.Miss)) await buffs.RecoverEnergyOnHitAsync(s, row);
            var fieldViewers = field != null ? field.Viewers : Enumerable.Empty<GameSession>();
            var sourceViewers = CurrentCaster() ? world.Viewers(s).Append(s) : Enumerable.Empty<GameSession>();
            foreach (var viewer in processed.SelectMany(t => t.Viewers).Concat(fieldViewers).Concat(sourceViewers).Concat(allies).Distinct()) await viewer.SendGameAsync(SkillHit, hit);
            if (CurrentCaster() && field?.Trap == true)
                await ToPlayerAndViewersAsync(s, TrapDamage, new TrapDamageNtf { SkillConfigId = action.Request.SkillId, DamageValue = hit.Dsts.Sum(d => d.Damage), Pos = center.Clone() });
        }
        foreach (var target in processed)
        {
            if (!damage) continue;
            if (target.Hp == 0 && !target.DeathDurable) continue; // another cast is still committing that death
            await world.ToUnitViewersAsync(target, AoiAttrChange, MonsterService.Hp(target));
            await FightingAsync(target);
            if (target.Hp > 0 && CurrentCaster()) monsters.Engage(target, s);
        }
        if (processed.Count > 0 && CurrentCaster()) await FightingAsync(s);
        foreach (var target in killed.Where(target => target.Hp == 0 && target.DeathDurable))
        {
            log.LogInformation("{Name} killed {Monster} ({Npc})", castingRole.Name, target.Uid, target.Template.NpcId);
            await monsters.KillAsync(target);
            foreach (var other in sessions.All().Where(o => o.Attack?.Target == target))
            {
                other.Attack = null;
                await other.SendGameAsync(SkillEnd, new SkillEndNotify
                {
                    SrcID = other.Role!.Id, SessionId = action.Id, Type = SkillEndNotify.Types.SkillEndType.Interrupted,
                    InterruptedInfo = new SkillInterruptedInfo { Type = SkillInterruptedInfo.Types.SkillInterruptType.LockerInvalid },
                });
            }
            await PublishDurableKillAsync(s, target, CurrentKillRecipient);
            await FinalizeLethalPublicationAsync(target);
        }
        }
        finally
        {
            foreach (var target in killed.Where(target => target.Hp == 0 && target.DeathDurable))
            {
                if (!target.Dead)
                    try { await monsters.KillAsync(target); }
                    catch (Exception ex) { log.LogError(ex, "Could not finalize reserved monster death for {Uid}", target.Uid); }
                // A later target/effect can fail after this source committed. Its room observers must still run.
                await PublishDurableKillAsync(s, target, CurrentKillRecipient);
                await FinalizeLethalPublicationAsync(target);
            }
        }
    }

    private bool RollBuff(JsonElement tuple, out int id, out int duration)
    {
        id = duration = 0;
        if (tuple.GetArrayLength() < 3) return false;
        id = (int)(GameData.Number(tuple[0]) ?? 0);
        duration = (int)(GameData.Number(tuple[2]) ?? 0);
        return id > 0 && Rolls.Proc(10000) < (GameData.Number(tuple[1]) ?? 0);
    }

    internal enum DisplaceKind { CasterForward, CasterBack, TargetPush, TargetPull }

    /// <summary>
    /// Skills whose show has a displacement EffectData (m_kPoint.m_bMovement) and whose description names the
    /// distance: _kDescData index, or -1 for a pull "in front of you". Who moves and which way comes from the
    /// description and the effect's curve name (击退 knockback, 拉人 pull); the server choice of skills is (inferred).
    /// ShieldsProtection's 1161341 knockback belongs to its periodic aura, which is not simulated.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, (DisplaceKind Kind, int DescIndex)> Displacements =
        new Dictionary<string, (DisplaceKind, int)>
        {
            ["BackSlide"] = (DisplaceKind.CasterBack, 0),
            ["RetreatingShadows"] = (DisplaceKind.CasterBack, 0),
            ["RetreatingShadowsS1"] = (DisplaceKind.CasterBack, 0),
            ["WolvesDash"] = (DisplaceKind.CasterForward, 0),
            ["HighspeedSlide"] = (DisplaceKind.CasterForward, 0),
            ["HighspeedSlideMecha"] = (DisplaceKind.CasterForward, 0),
            ["PinpointAttack"] = (DisplaceKind.CasterForward, 3),
            ["ArrowRepel"] = (DisplaceKind.TargetPush, 3),
            ["PileBunker"] = (DisplaceKind.TargetPush, 5),
            ["PhantomThrust"] = (DisplaceKind.TargetPull, -1),
        };

    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, (int Id, int Ms)> _movementEffects = new();

    /// <summary>
    /// The show's displacement EffectData: the lowest id in [scriptShow*100, scriptShow*100+99] with m_kPoint.m_bMovement
    /// (BackSlide 15104 -> 1510403, PileBunker 16314 -> 1631405 knockback). The client's SkillStartMove ignores a 31112
    /// without it (LC_PerformEffect_Agent.lua SkillStartMove returns on effectId nil/0).
    /// </summary>
    internal (int Id, int Ms) MovementEffect(long scriptShow) => _movementEffects.GetOrAdd(scriptShow, show =>
    {
        var (id, ms, _) = FindMovementEffect(data, show);
        return (id, ms);
    });

    /// <summary>The show's displacement EffectData (see <see cref="MovementEffect"/>), with its move curve's name.</summary>
    internal static (int Id, int Ms, string Curve) FindMovementEffect(GameData data, long show)
    {
        var effects = data.SkillTemplateTable("Lua_Config_EffectData");
        for (long id = show * 100; id < show * 100 + 100; id++)
            if (effects.TryGetValue(id.ToString(), out var e) && e.TryGetProperty("m_kPoint", out var point)
                && point.TryGetProperty("m_bMovement", out var moves) && moves.ValueKind == JsonValueKind.True)
                return ((int)id, point.TryGetProperty("m_iMovementTime", out var ms) && ms.TryGetInt32(out int t) ? t : 0,
                    point.TryGetProperty("m_kMoveCurveName", out var curve) && curve.ValueKind == JsonValueKind.String ? curve.GetString() ?? "" : "");
        return (0, 0, "");
    }

    private (DisplaceKind Kind, float Distance, int EffectId, int Ms)? Displacement(JsonElement row)
    {
        if (!Displacements.TryGetValue(Script(row), out var rule)) return null;
        var (effect, ms) = MovementEffect(GameData.Int(row, "_iScriptShow"));
        if (effect == 0) return null;
        float distance = rule.DescIndex < 0 ? 1 // (inferred) "pulling them in front of you": 1 m ahead of the caster
            : (float)Numbers(row, "_kDescData").ElementAtOrDefault(rule.DescIndex);
        return distance > 0 ? (rule.Kind, distance, effect, ms) : null;
    }

    private async Task MoveCasterAsync(GameSession s, ActiveSkillCast action)
    {
        if (Displacement(action.Row) is not { Kind: DisplaceKind.CasterForward or DisplaceKind.CasterBack } move) return;
        float face = action.Request.Face + (move.Kind == DisplaceKind.CasterBack ? 180 : 0);
        double angle = face * Math.PI / 180;
        var end = WalkableEnd(s.SceneEntry, s.Position, (float)Math.Sin(angle) * move.Distance, (float)Math.Cos(angle) * move.Distance);
        var start = s.Position.Clone();
        await ToPlayerAndViewersAsync(s, SkillMove, new SkillStartMoveNotify
        {
            EntityID = (ulong)s.Role!.Id, StartPos = start, EndPos = end, SkillConfigId = action.Request.SkillId,
            EffectId = move.EffectId, StartTimestampMs = Now, MoveType = 1, Face = action.Request.Face,
            MoveTimeMs = move.Ms > 0 ? move.Ms : action.ActionMs,
        });
        // MoveAsync updates persistent position and AOI membership from the same authoritative destination.
        s.PreviousPosition = start;
        s.Position = end;
        await world.MoveAsync(s, new WalkReq { Pos = end, Dir = s.Face, Face = s.Face, BStop = true }, forceTeleport: true);
    }

    private async Task DisplaceTargetAsync(GameSession s, ActiveSkillCast action, MonsterUnit target)
    {
        if (Displacement(action.Row) is not { Kind: DisplaceKind.TargetPush or DisplaceKind.TargetPull } move) return;
        var from = s.Position;
        float dx = target.X - from.X, dz = target.Z - from.Z;
        float length = MathF.Sqrt(dx * dx + dz * dz);
        if (length < 0.001f) return;
        // A push goes away from the caster; a pull stops the given distance in front of it.
        float distance = move.Kind == DisplaceKind.TargetPush ? move.Distance : -(length - move.Distance);
        if (move.Kind == DisplaceKind.TargetPull && length <= move.Distance) return;
        var start = target.Pos;
        var end = WalkableEnd(s.SceneEntry, start, dx / length * distance, dz / length * distance);
        target.Moving = false;
        target.X = end.X; target.Y = end.Y; target.Z = end.Z;
        await world.ToUnitViewersAsync(target, SkillMove, new SkillStartMoveNotify
        {
            EntityID = (ulong)target.Uid, StartPos = start, EndPos = end, SkillConfigId = action.Request.SkillId,
            EffectId = move.EffectId, MoveType = 2, StartTimestampMs = Now, Face = target.Face,
            MoveTimeMs = move.Ms > 0 ? move.Ms : 300,
        });
        await world.MoveUnitAsync(target, new MovementChangeNtf { Uid = target.Uid, MovementInfo = target.CurrentMovement(Now) });
    }

    private Vec3 WalkableEnd(long sceneId, Vec3 start, float dx, float dz)
    {
        var nav = worldData.Map(sceneId)?.Nav;
        var end = start.Clone();
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / 0.25));
        for (int i = 1; i <= steps; i++)
        {
            float x = start.X + dx * i / steps, z = start.Z + dz * i / steps;
            if (nav != null && !nav.CanWalk(end.X, end.Y, end.Z, x, z)) break;
            end = new Vec3 { X = x, Y = nav?.WalkHeight(x, z, end.Y) ?? end.Y, Z = z };
        }
        return end;
    }

    /// <summary>
    /// Owned ground triggers use NPCConfig identities, not WidgetConfig. Fire_Wall/Safety_Wall/Pneuma tuples explicitly
    /// reference NPCs; Hunter trap NPCs match their localized skill names. Timing/limits below are inferred from the
    /// corresponding descriptions and _kLogicData, documented per family in docs/systems/skills-rest.md.
    /// </summary>
    private async Task<bool> CreateSkillObjectAsync(GameSession s, ActiveSkillCast action)
    {
        if (!CurrentCastMap(s, action)) return true;
        var row = action.Row;
        string script = Script(row);
        var logic = Numbers(row, "_kLogicData");
        int npc = 0, duration = 0, interval = 1000, protection = 0, charges = 0;
        bool trap = false;
        float radius = (float)(Numbers(row, "_kRange").FirstOrDefault(150) / 100);
        switch (script)
        {
            case "Fire_Wall" or "Quagmire" or "Fire_Pillar" or "Meteor_Storm" or "Lord_of_Vermilion" or "Storm_Gust" when logic.Length >= 3:
                npc = (int)logic[0]; interval = (int)logic[1]; duration = (int)logic[2]; break;
            case "Magnus_Exorcismus":
                npc = logic.Length > 0 && logic[0] > 0 ? (int)logic[0] : 22000009;
                interval = 500;
                duration = logic.Length > 1 && logic[1] > 0 ? (int)(logic[1] * 1000) : 4000;
                radius = 8f;
                break;
            case "Sanctuary":
                npc = 22000003;
                interval = 600;
                duration = 15000;
                radius = 7f;
                break;
            case "Safety_Wall" or "Pneuma" when logic.Length >= 8:
                npc = (int)logic[5]; duration = (int)logic[2]; radius = (float)(logic[7] / 100);
                protection = script == "Safety_Wall" ? 1 : 2; charges = script == "Safety_Wall" ? (int)logic[1] : int.MaxValue; break;
            case "LandMine": npc = 22000016; trap = true; break;
            case "ClaymoreTrap": npc = 22000017; trap = true; break;
            case "FreezingTrap": npc = 22000018; trap = true; break;
            case "BlastMine": npc = 22000019; trap = true; break;
            default:
                if (script.Contains("Summon", StringComparison.OrdinalIgnoreCase))
                {
                    npc = logic.Select(v => (int)v).FirstOrDefault(id => data.Row("NPCConfig", id) != null);
                    duration = 30000; // (inferred) bounded lifecycle for a named summon without an explicit lifetime tuple.
                }
                break;
        }
        if (npc == 0 || data.Row("NPCConfig", npc) is not { } npcRow) return false;
        if (trap)
        {
            var desc = row.TryGetProperty("_kDescData", out var d) && d.ValueKind == JsonValueKind.Array ? d.EnumerateArray().ToArray() : [];
            duration = desc.Length > 0 && double.TryParse(desc[^1].ToString(), System.Globalization.CultureInfo.InvariantCulture, out double seconds) ? (int)(seconds * 1000) : 20000;
            radius = 3; interval = 100;
        }
        var point = action.Request.TargetPos ?? action.Origin;
        var unit = new SkillObject
        {
            Uid = ++_skillObjectIds, SceneEntry = s.SceneEntry, InstanceId = s.InstanceId, RuntimeMap = s.RuntimeMap,
            OwnerSession = s, OwnerId = s.Role!.Id,
            NpcId = npc, EntityType = (int)GameData.Int(npcRow, "_iNPCType"), Cast = action,
            X = point.X, Y = point.Y, Z = point.Z, Face = action.Request.Face, ExpiresMs = Now + Math.Max(1, duration),
            IntervalMs = Math.Max(50, interval), NextTickMs = Now, Radius = radius, Trap = trap,
            ProtectionDistanceType = protection, Charges = charges,
        };
        foreach (int skill in Numbers(npcRow, "_kSkills").Select(v => (int)v)) unit.Skills.Add(skill);
        s.SkillObjects.Add(unit);
        await world.AddUnitAsync(unit);
        if (!CurrentCastMap(s, action)) { await RemoveSkillObjectAsync(unit); return true; }
        foreach (var tuple in Tuples(row, "_kBuff1"))
            if (RollBuff(tuple, out int buffId, out int buffDuration)) await buffs.AddAsync(s, buffId, buffDuration, s.Role.Id);
        return true;
    }

    private async Task TickSkillObjectAsync(SkillObject unit)
    {
        if (unit.Busy || unit.Removing) return;
        unit.Busy = true;
        try
        {
            var owner = unit.OwnerSession;
            if (Now >= unit.ExpiresMs || owner.Role?.Id != unit.OwnerId || owner.SceneLoading || owner.Dead
                || owner.WorldScene != unit.WorldScene || owner.SceneGeneration != unit.Cast.SceneGeneration
                || !ReferenceEquals(owner.Attachment, unit.Cast.Attachment) || unit.Cast.Attachment?.Cancelled == true)
            {
                await RemoveSkillObjectAsync(unit);
                return;
            }
            if (Now < unit.NextTickMs) return;
            unit.NextTickMs = Now + unit.IntervalMs;
            if (unit.ProtectionDistanceType != 0) return;
            if (unit.Trap && !world.Map(unit.WorldScene).Near(unit.X, unit.Z, 1.5f).OfType<MonsterUnit>().Any(m => !m.Dead && Distance(m.Pos, unit.Pos) <= 1.5f)) return;
            await ApplyEffectsAsync(owner, unit.Cast, unit.Pos, unit);
            if (unit.Trap) await RemoveSkillObjectAsync(unit);
        }
        finally { unit.Busy = false; }
    }

    private async Task RemoveSkillObjectAsync(SkillObject unit)
    {
        if (unit.Removing) return;
        unit.Removing = true;
        unit.OwnerSession.SkillObjects.Remove(unit);
        await world.RemoveUnitAsync(unit);
    }

    /// <summary>Scene/role/disconnect teardown invalidates callbacks before despawning owned skill entities.</summary>
    public async Task ClearSkillsAsync(GameSession s)
    {
        eventAttacks.Remove(s);
        s.WorldState.ResurrectionCastId = 0;
        s.Attack = null;
        if (s.SkillCast is { } cast) cast.Cancelled = true;
        s.SkillCast = null;
        foreach (var unit in s.SkillObjects.ToArray()) await RemoveSkillObjectAsync(unit);
    }

    private async Task<bool> GroundProtectsAsync(GameSession target, JsonElement? skill)
    {
        int distanceType = skill is { } row ? (int)GameData.Int(row, "_iDistanceType") : 1;
        var field = world.Map(target.WorldScene).Units.OfType<SkillObject>().FirstOrDefault(u => !u.Removing && u.ExpiresMs > Now
            && u.ProtectionDistanceType == distanceType && u.Charges > 0 && Distance(u.Pos, target.Position) <= u.Radius);
        if (field == null) return false;
        if (--field.Charges == 0) await RemoveSkillObjectAsync(field);
        return true;
    }
}
