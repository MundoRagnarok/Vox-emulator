using System.Text.Json;
using Romsg;

namespace Ro3Emu.Server.Game;

/// <summary>
/// Player targets for the configured Kafra/Dragon private matches. The event owner supplies hostility and
/// settlement; casts reuse SkillConfig ownership, SP, cooldown, range, hit moment, shared damage, HP and death.
/// Reusing the emulator's inferred damage formula for player defenders is (inferred); scope never extends to
/// ordinary world scenes or arbitrary player ids. AOEs share one configured target cap with monster targets.
/// </summary>
public sealed partial class CombatService
{
    private sealed record EventAttack(GameSession Target, int SkillId, long NextSwing, float StartX, float StartZ);
    private readonly Dictionary<GameSession, EventAttack> eventAttacks = new();
    private Func<GameSession, GameSession, bool>? eventCanFight;
    private Func<GameSession, GameSession, Task>? eventPlayerKilled;

    public void SetEventPvPHandlers(Func<GameSession, GameSession, bool> canFight,
        Func<GameSession, GameSession, Task> playerKilled)
    { eventCanFight = canFight; eventPlayerKilled = playerKilled; }

    private bool CanEventHit(GameSession source, GameSession target) => source != target
        && source.Role != null && target.Role != null && !source.Dead && !target.Dead
        && !source.SceneLoading && !target.SceneLoading && source.InstanceId != 0
        && source.InstanceId == target.InstanceId && eventCanFight?.Invoke(source, target) == true;

    private bool CanPlayerHit(GameSession source, GameSession target) =>
        CanEventHit(source, target) || CanTerritoryHit(source, target);

    private GameSession? EventTarget(GameSession source, long id) => sessions.All()
        .FirstOrDefault(target => target.Role?.Id == id && CanPlayerHit(source, target));

    private static float PlayerDistance(GameSession a, GameSession b) =>
        MonsterService.Distance(a.Position.X, a.Position.Z, b.Position.X, b.Position.Z);

    private GameSession[] EventAreaTargets(GameSession source, JsonElement row)
    {
        float radius = (row.TryGetProperty("_kRange", out var range) && range.ValueKind == JsonValueKind.Array
            && range.GetArrayLength() > 0 ? (float)(Data.GameData.Number(range[0]) ?? 300) : 300) / 100f;
        return sessions.All().Where(p => CanPlayerHit(source, p) && PlayerDistance(source, p) <= radius + .5f).ToArray();
    }

    private async Task SwingEventAsync(GameSession source, GameSession target, int skillId, long session)
    {
        if (source.Role == null || source.Progress == null) return;
        using var held = await CharacterService.Lock(source);
        if (!CanPlayerHit(source, target) || source.SkillCast is { Cancelled: false }
            || EffectiveSkillRow(source, skillId) is not { } row || !Learned(source, row)
            || !SkillModeAllowed(source, row) || buffs.HasForbidden(source, 5)) return;
        var request = new CastSkillReq
        {
            SkillId = (int)Data.GameData.Int(row, "_iID"), TargetId = target.Role!.Id,
            TargetPos = target.Position.Clone(), Face = MonsterService.Angle(source.Position.X, source.Position.Z, target.Position.X, target.Position.Z),
        };
        if (!await AdmitAutomaticCostAsync(source, row, request, session)) { eventAttacks.Remove(source); return; }
        if (Data.GameData.Int(row, "_iIsNormalAtk") == 1)
            source.NextNormalAttackMs = Now + SkillMs(source, row);
        await StartCastAsync(source, row, request, session);
    }
    private async Task HitEventPlayersAsync(GameSession source, GameSession[] targets, JsonElement row,
        int skillId, int show, long session, Func<bool> currentSource)
    {
        if (FirstNumber(row, "_kDamageParam1") is not > 0 && FirstNumber(row, "_kDamageParam2") is not > 0) return;
        foreach (var target in targets)
        {
            if (!currentSource()) break;
            if (!CanPlayerHit(source, target)) continue;
            bool eventFight = CanEventHit(source, target);
            var attackerRole = source.Role!;
            var targetRole = target.Role!;
            long sceneId = source.WorldScene;
            var attack = character.Stats(source); var defend = character.Stats(target);
            var a = new Fighter(attack[103], attack[104], attack[109], attack[111], source.Progress!.Level, 1);
            var b = new Fighter(0, 0, 0, 0, target.Progress!.Level, 1)
            { Def = defend[105], Mdef = defend[106], Flee = defend[110], ResCri = defend[112] };
            var (damage, result) = Damage(a, b, row);
            if (result != DamageHitResult.Miss) damage = ApplyPlayerRestraints(source, target, damage);
            var committed = await CommitPlayerDamageAsync(target, damage, row,
                () => currentSource() && CurrentPlayerHit(source, target, attackerRole, targetRole, sceneId)
                    && CanPlayerHit(source, target));
            if (committed == null) continue;
            (damage, long hp, long effective, long shieldDamage) = committed.Value;
            try
            {
            if (effective > 0 && CurrentPlayerHit(source, target, attackerRole, targetRole, sceneId))
            {
                foreach (var handler in PvpDealt) handler(source, target, effective, hp == 0);
                foreach (var handler in PvpDealtAsync) await handler(source, target, effective, hp == 0);
            }
            var hit = new SkillHitInfo { SrcID = attackerRole.Id, SkillId = skillId, ShowPoint = show,
                SessionId = session, SrcElement = 1,
                Dsts = { new DamageInfo { DstID = targetRole.Id, Damage = damage, SheildDamage = shieldDamage, HitResult = result,
                    DstElement = 1, IsDead = hp == 0 } } };
            var sourceViewers = CurrentPlayerHit(source, target, attackerRole, targetRole, sceneId)
                ? world.Viewers(source).Append(source) : Enumerable.Empty<GameSession>();
            foreach (var viewer in sourceViewers.Concat(world.Viewers(target)).Append(target).Distinct())
                await viewer.SendGameAsync(SkillHit, hit);
            if (!ReferenceEquals(target.Role, targetRole)) continue;
            await character.SetHpSpAsync(target, null, null); // publish current HP after the serialized reservation
            if (!ReferenceEquals(target.Role, targetRole)) continue;
            if (CurrentPlayerHit(source, target, attackerRole, targetRole, sceneId)) await FightingAsync(source);
            await FightingAsync(target);
            if (hp == 0)
            {
                if (!ReferenceEquals(target.Role, targetRole)) continue;
                await PlayerDiesAsync(target);
                if (effective <= 0 || !CurrentPlayerHit(source, target, attackerRole, targetRole, sceneId)) continue;
                if (eventFight && eventPlayerKilled != null) await eventPlayerKilled(source, target);
                foreach (var handler in PlayerKilled)
                {
                    if (!CurrentPlayerHit(source, target, attackerRole, targetRole, sceneId)) break;
                    await handler(source, target);
                }
            }
            }
            finally { if (hp == 0) ReleaseDeathPublication(target); }
        }
    }

    private void TickEventPvP(long now)
    {
        foreach (var (source, attack) in eventAttacks.ToArray())
        {
            if (!CanPlayerHit(source, attack.Target) || PlayerDistance(source, attack.Target) > 30
                || MonsterService.Distance(attack.StartX, attack.StartZ, source.Position.X, source.Position.Z) > 1.5f)
            { eventAttacks.Remove(source); continue; }
            if (now < attack.NextSwing || source.SkillCast is { Cancelled: false } || data.Row("SkillConfig", attack.SkillId) is not { } row
                || PlayerDistance(source, attack.Target) > Range(row) + 1.5f) continue;
            eventAttacks[source] = attack with { NextSwing = now + SkillMs(source, row) };
            loop.PostRuntime(() => SwingEventAsync(source, attack.Target, attack.SkillId, ++_sessionIds));
        }
    }
}
