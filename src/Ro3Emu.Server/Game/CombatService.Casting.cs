using System.Text.Json;
using Romsg;
using Ro3Emu.Server.Data;
using Ro3Emu.Server.Maps;
using Ro3Emu.Server.Game.Ownership;

namespace Ro3Emu.Server.Game;

/// <summary>A single accepted cast, bound to its role, scene and generation (LC_Scene_Sync._skillEndNotify).</summary>
public sealed class ActiveSkillCast
{
    public required long Id { get; init; }
    public required long OwnerId { get; init; }
    public required long WorldScene { get; init; }
    public long SceneGeneration { get; init; }
    public Ro3Emu.Server.Game.Ownership.AttachmentLease? Attachment { get; init; }
    public required CastSkillReq Request { get; init; }
    public required JsonElement Row { get; init; }
    public required Vec3 Origin { get; set; }
    /// <summary>Accepted while the caster still walked (WalkSlack): its stop right after is where it cast from.</summary>
    public bool StartedWalking { get; set; }
    public required long StartMs { get; init; }
    public required int ChantMs { get; init; }
    public required int ActionMs { get; init; }
    public bool Cancelled { get; set; }
    public bool Resolved { get; set; }
    public SkillObject? Operator { get; init; }
    public long SourceId => Operator?.Uid ?? OwnerId;
}

public sealed partial class CombatService
{
    public const uint SkillMove = 31112, TrapDamage = 31128;

    /// <summary>
    /// 31102: row identity, ownership, selector, mode, all costs and cooldowns are checked before the cast commits.
    /// Target/position come from CastSkillReq; m_kSelectInfo uses relation masks, never _iDmgType as a selector.
    /// Evidence: LC_Skill_Function.SendSkill, SkillConfig.GetCost*, MG_Skill.proto.
    /// </summary>
    public async Task CastAsync(GameSession s, CastSkillReq request)
    {
        LegacyOwnerScope? admission = null;
        OwnerNotices? notices = null;
        if (s.Owners is { } owners && s.Role is { } role)
            admission = await owners.EnterWithGateAsync("combat/cast", s.SkillCastGate, [ReservationKey.Role(role.Id)]);
        else await s.SkillCastGate.WaitAsync();
        try
        {
            using var state = await CharacterService.Lock(s);
            // Complete the runtime admission before any socket send can suspend. Frozen packets
            // retain the original order; an obsolete attachment cannot start a committed cast.
            if (s.Owners?.Current is { Notices: null } context) notices = new OwnerNotices(context);
            await CastCoreAsync(s, request);
        }
        finally { notices?.Dispose(); s.SkillCastGate.Release(); admission?.Dispose(); }
        if (notices != null) await notices.FlushAsync();
    }

    private async Task CastCoreAsync(GameSession s, CastSkillReq request)
    {
        var req = request.Clone();
        var row = EffectiveSkillRow(s, req.SkillId);
        var owned = req.OpUnitId == 0 || req.OpUnitId == s.Role?.Id ? null
            : s.SkillObjects.FirstOrDefault(u => u.Uid == req.OpUnitId && u.WorldScene == s.WorldScene && u.ExpiresMs > Now);
        int error = s.Role == null || s.Progress == null || s.Dead || s.SceneLoading ? ErrorCodes.SkillBadState
            : row == null ? ErrorCodes.SkillUnknown
            : req.OpUnitId != 0 && req.OpUnitId != s.Role.Id && owned == null ? ErrorCodes.SkillRequirements
            : !float.IsFinite(req.Face) || req.TargetPos is { } pos && !Finite(pos) ? ErrorCodes.Parameter : 0;
        if (error == 0)
        {
            req.SkillId = (int)GameData.Int(row!.Value, "_iID");
            if (owned != null ? !owned.Skills.Contains(req.SkillId) : !Learned(s, row!.Value)) error = ErrorCodes.SkillNotLearned;
            else if (!SkillModeAllowed(s, row!.Value)) error = ErrorCodes.SkillRequirements;
            else if (GameData.Int(row!.Value, "_iBasicType") != 1) error = ErrorCodes.SkillPassive;
            else if (buffs.HasForbidden(s, GameData.Int(row.Value, "_iIsNormalAtk") == 1 ? 5 : 4)) error = ErrorCodes.SkillBadState;
            else if (req.SkillId == PublicSkill("WorldTreeSkillId") && buffs.HasForbidden(s, 12)) error = ErrorCodes.ItemUseForbidden;
            else if (req.SkillId == PublicSkill("WorldTreeSkillId") && !ItemAllowedInMap(s, (int)PublicSkill("WorldTreeItemId"))) error = ErrorCodes.ItemUseAreaForbidden;
            else if (req.IsExtraSkill && owned == null && GameData.Int(row.Value, "_iExtraType") == 0) error = ErrorCodes.SkillRequirements;
        }
        var origin = owned?.Pos ?? s.Position;
        if (error == 0)
        {
            int group = (int)GameData.Int(row!.Value, "_iSkillID"), mode = (int)GameData.Int(row.Value, "_iMode");
            bool isNormal = GameData.Int(row.Value, "_iIsNormalAtk") == 1;
            if (!s.NoCd && Math.Max(s.SkillCds.GetValueOrDefault(group), s.SkillModeCds.GetValueOrDefault((group, mode))) > Now) error = ErrorCodes.SkillCooldown;
            else if (!s.NoCd && isNormal && s.NextNormalAttackMs > Now)
            {
                if (s.Attack != null && req.TargetId != 0 && req.TargetId != s.Attack.Target.Uid)
                {
                    var newTarget = FindMonster(s.WorldScene, req.TargetId);
                    if (newTarget != null && CanHitMonster(s, newTarget))
                    {
                        s.Attack = new AttackLoop { Target = newTarget, SkillId = s.Attack.SkillId,
                            NextSwingMs = s.Attack.NextSwingMs, StartX = s.Position.X, StartZ = s.Position.Z };
                    }
                }
                error = ErrorCodes.SkillBusy;
            }
            // Preserve the established cooldown refusal for a repeated cast, including during its action tail.
            else if (s.SkillCast is { Cancelled: false } active && Now < active.StartMs + active.ChantMs + active.ActionMs
                && (isNormal || GameData.Int(row.Value, "_bCancelCast") == 0 || GameData.Int(active.Row, "_bCancelCast") == 0)) error = ErrorCodes.SkillBusy;
            else error = ValidateTarget(s, row.Value, req, origin, owned == null ? WalkSlack(s) : 0);
        }
        var costs = row is { } value ? ResolveCosts(s, value) : new CastCosts();
        long errorParam = 0;
        if (error == 0) (error, errorParam) = ValidateCosts(s, costs);
        if (error == 0 && !buffs.CanSpendEnergy(s, row!.Value)) error = ErrorCodes.SkillEnergyShort;
        if (error != 0)
        {
            await RefuseCastAsync(s, req, error, errorParam);
            return;
        }

        long id = ++_sessionIds;
        var lease = s.Attachment;
        long scene = s.SceneGeneration, worldScene = s.WorldScene;
        // E05c: the receipt is the paid action's admission. Repeating a protocol request
        // is a new intent and pays again; a lost writer ACK resolves this exact source.
        if (!await AdmitCastCostAsync(s, row!.Value, req, costs, id))
        {
            await RefuseCastAsync(s, req, ErrorCodes.SkillCostShort, costs.Items[0].Entry);
            return;
        }
        if (lease?.Cancelled == true || !ReferenceEquals(s.Attachment, lease) ||
            s.SceneGeneration != scene || s.WorldScene != worldScene || s.SceneLoading) return;
        await CancelCastAsync(s, SkillInterruptedInfo.Types.SkillInterruptType.Skill);
        eventAttacks.Remove(s);
        await PayCostsAsync(s, costs);
        await buffs.SpendEnergyAsync(s, row!.Value);
        await s.SendGameAsync(UseSkill, new CastSkillRsp { SessionId = id, IsAutoCast = req.IsAutoCast });
        var target = FindMonster(s.WorldScene, req.TargetId);
        var playerTarget = EventTarget(s, req.TargetId);
        // _iIfAutoAtk 2 (enum_AutoNormalAttackType.AutoNormalAttack) follows the cast with the career's normal attack
        // (LC_AutoAttackAppendState: GetNormalSkill). Repeating the cast skill itself bypassed its cooldown: Blade
        // Smash and Sonic Slash fired every swing.
        int loopSkill = GameData.Int(row!.Value, "_iIsNormalAtk") == 1 ? req.SkillId : skills.NormalAttack(s.Progress!.Career);
        if (GameData.Int(row!.Value, "_iIfAutoAtk") == 2 && owned == null && target != null && CanHitMonster(s, target))
        {
            s.Attack = new AttackLoop { Target = target, SkillId = loopSkill,
                NextSwingMs = Now + SkillMs(s, row.Value) + Math.Max(0, GameData.Int(row.Value, "_iChantFix") + GameData.Int(row.Value, "_iChantVar")) + 1,
                StartX = s.Position.X, StartZ = s.Position.Z };
        }
        else s.Attack = null;
        if (GameData.Int(row.Value, "_iIfAutoAtk") == 2 && owned == null && playerTarget != null)
            eventAttacks[s] = new EventAttack(playerTarget, loopSkill,
                Now + SkillMs(s, row.Value) + Math.Max(0, GameData.Int(row.Value, "_iChantFix") + GameData.Int(row.Value, "_iChantVar")) + 1,
                s.Position.X, s.Position.Z);
        if (GameData.Int(row.Value, "_iIsNormalAtk") == 1)
            s.NextNormalAttackMs = Now + SkillMs(s, row.Value);
        await StartCooldownAsync(s, row.Value);
        await StartCastAsync(s, row.Value, req, id, owned);
    }

    /// <summary>Cast item cost and frozen intent share one owner receipt; HP/SP/buffs remain runtime resources.
    /// The admission uses the same scheduled cast and lethal pipeline as every ordinary skill.</summary>
    private async Task<bool> AdmitCastCostAsync(GameSession s, JsonElement row, CastSkillReq request, CastCosts costs, long id)
    {
        if (costs.Items.Count == 0) return true;
        var mutation = character.ProgressionEconomy(s, "cd13c/cast-admit", costs.Items, [],
            new { SessionId = id, Skill = row.GetRawText(), Request = Convert.ToBase64String(Google.Protobuf.MessageExtensions.ToByteArray(request)),
                Plan = s.SkillPlans?.CurrentPlanIndex, Attachment = s.Attachment?.Generation,
                s.WorldScene, s.SceneGeneration, AttributeCosts = costs.Attrs, BuffCosts = costs.Buffs,
                EnergyCosts = costs.Energy, Outcome = "admitted" });
        return await character.ExchangeLockedAsync(s, costs.Items, [], popup: false,
            persist: tx => tx.WithEconomy(mutation), asyncCommit: true) != null;
    }

    private Task RefuseCastAsync(GameSession s, CastSkillReq req, int error, long parameter) => RefuseCoreAsync(s, req, error, parameter);

    // Refusal must not interrupt a different accepted cast: the client does not compare 31103.sessionId.
    private async Task RefuseCoreAsync(GameSession s, CastSkillReq req, int error, long parameter)
    {
        var reply = new CastSkillRsp { Result = error, IsAutoCast = req.IsAutoCast };
        if (parameter != 0) reply.Params.Add(parameter);
        await s.SendGameAsync(UseSkill, reply);
        if (s.SkillCast is not { Cancelled: false })
        {
            var end = new SkillEndNotify { SrcID = s.Role?.Id ?? 0, Type = SkillEndNotify.Types.SkillEndType.Interrupted, Result = error };
            if (parameter != 0) end.Params.Add(parameter);
            await s.SendGameAsync(SkillEnd, end);
        }
    }

    private bool Learned(GameSession s, JsonElement row)
    {
        if (GameData.Int(row, "_iID") == PublicSkill("WorldTreeSkillId")) return true; // paid item skill, LC_ReviveHelper.
        // A Survivor Battle career owns the match skill set (TimedBattleService.Chicken), not the job tree.
        if (MatchSkillRule?.Invoke(s, (int)GameData.Int(row, "_iID")) is { } granted) return granted;
        if (GameData.Int(row, "_iID") == skills.NormalAttack(s.Progress!.Career)) return true;
        return skills.Level(s.Progress, (int)GameData.Int(row, "_iSkillID")) >= GameData.Int(row, "_iLevel");
    }

    private MonsterUnit? FindMonster(long sceneId, long uid) => world.Map(sceneId).Units.OfType<MonsterUnit>().FirstOrDefault(m => m.Uid == uid && !m.Dead);
    private GameSession? FindPlayer(GameSession s, long uid, bool dead = false) => world.PlayersIn(s.WorldScene).FirstOrDefault(p => p.Role?.Id == uid && p.Dead == dead);
    private long PublicSkill(string key) => data.Table("PublicParamConfig").TryGetValue(key, out var value) ? (long)(GameData.Number(value) ?? 0) : 0;
    private static bool Finite(Vec3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
    private static float Distance(Vec3 a, Vec3 b) => MonsterService.Distance(a.X, a.Z, b.X, b.Z);
    private static float Range(JsonElement row) => Math.Max(0, GameData.Int(row, "_iDistanceMax") / 100f);
    private static string Script(JsonElement row) => row.TryGetProperty("_kScriptLogic", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static double[] Numbers(JsonElement row, string key) => row.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array
        ? list.EnumerateArray().Select(v => GameData.Number(v) ?? 0).ToArray() : [];
    private static IEnumerable<JsonElement> Tuples(JsonElement row, string key) => row.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array
        ? list.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Array) : [];

    private bool Relation(JsonElement row, bool friendly, bool self = false, int race = 0, bool dead = false)
    {
        var template = data.SkillTemplate(GameData.Int(row, "_iScriptShow"));
        if (template is not { } t || !t.TryGetProperty("m_kSelectInfo", out var selectors) || selectors.ValueKind != JsonValueKind.Array)
            return friendly == (GameData.Int(row, "_iDmgType") == 3); // (inferred) typed recovery fallback for rows without a visual selector.
        foreach (var select in selectors.EnumerateArray())
        {
            int relation = (int)GameData.Int(select, "m_kSelectType"), subtype = (int)GameData.Int(select, "m_kSelectSubType");
            if ((relation & (dead ? friendly ? 32 : 16 : friendly ? 2 : 1)) == 0 && (relation & (dead ? 64 : 4)) == 0) continue;
            if (subtype == 2 && !self || subtype == 16 && self) continue;
            long raceMask = GameData.Int(select, "m_kSelectRaceType");
            if (!friendly && raceMask > 0 && (race <= 0 || (raceMask & (1L << (race - 1))) == 0)) continue;
            return true;
        }
        return false;
    }

    // The client casts on reaching range while it still walks; its stop arrives just after the cast (in-game 11.10:
    // 13 ms later, refused 3024 "invalid skill distance" on most approaches). The cast carries no caster position, so
    // a walking caster's range allows the distance it can have covered since its last walk, at most 0.75 s of it.
    private static float WalkSlack(GameSession s) => !s.Walking ? 0
        : (float)(s.Speed / 100.0 * Math.Clamp((DateTime.UtcNow - s.LastWalkUtc).TotalSeconds, 0, 0.75));

    private int ValidateTarget(GameSession s, JsonElement row, CastSkillReq req, Vec3 origin, float slack = 0)
    {
        int release = (int)GameData.Int(row, "_iRelease");
        var monster = FindMonster(s.WorldScene, req.TargetId);
        bool revive = Script(row) == "Resurrection";
        var player = req.TargetId == s.Role!.Id ? s : FindPlayer(s, req.TargetId, revive);
        var enemyPlayer = EventTarget(s, req.TargetId);
        Vec3 targetPosition;
        if (release == 1)
        {
            if (monster != null && CanHitMonster(s, monster) && Relation(row, false, race: monster.Template.Race)) targetPosition = monster.Pos;
            else if (!revive && enemyPlayer != null) targetPosition = enemyPlayer.Position;
            else if (player != null && player.Dead == revive && Relation(row, true, player == s, dead: revive)
                && (!revive || !buffs.HasForbidden(player, 16))) targetPosition = player.Position;
            else if (req.TargetId == 0 && Relation(row, true, true)) { req.TargetId = s.Role.Id; targetPosition = s.Position; }
            else return ErrorCodes.SkillTargetInvalid;
            req.TargetPos = targetPosition.Clone();
            if (revive)
            {
                if (player == s) return ErrorCodes.SkillTargetInvalid;
                if (req.SkillId == PublicSkill("WorldTreeSkillId") ? ReviveItemSkill == null : ReviveOffer == null)
                    return ErrorCodes.SkillReviveNotAllowed;
                if (ReviveCheck?.Invoke(player!) is { } reviveError && reviveError != 0) return reviveError;
            }
        }
        else if (release is 7 or 8)
        {
            if (req.TargetPos == null) return ErrorCodes.SkillTargetInvalid;
            targetPosition = req.TargetPos;
        }
        else { req.TargetPos ??= origin.Clone(); return 0; }
        float d = Distance(origin, targetPosition);
        if (d < GameData.Int(row, "_iDistanceMin") / 100f - slack || d > Range(row) + 0.5f + slack) return ErrorCodes.SkillDistance;
        float height = GameData.Int(row, "_iHeightRange") / 100f;
        if (height > 0 && MathF.Abs(origin.Y - targetPosition.Y) > height) return ErrorCodes.SkillHeight;
        if (GameData.Int(row, "_iPassBlock") == 2 && worldData.Map(s.SceneEntry)?.Nav is { } nav
            && !nav.CanWalk(origin.X, origin.Y, origin.Z, targetPosition.X, targetPosition.Z)
            && (release != 1 || monster == null
                || BlockedMonsterEndpointAllowed?.Invoke(s, monster, origin, targetPosition) != true))
            return ErrorCodes.SkillBlocked;
        return 0;
    }

    /// <summary>LC_ItemHelper.ItemCanUseInCurrentMap: MapConfig type/subtype blacklist (1) or whitelist (2).</summary>
    private bool ItemAllowedInMap(GameSession s, int entry)
    {
        if (data.Row("MapConfig", s.SceneEntry) is not { } map || data.Row("ItemConfig", entry) is not { } item) return false;
        int mode = (int)GameData.Int(map, "_iLimitType");
        if (mode is not (1 or 2)) return true;
        bool listed = Tuples(map, "_kLimitItem").Any(t => t.GetArrayLength() >= 2 && GameData.Number(t[1]) ==
            GameData.Int(item, GameData.Number(t[0]) == 1 ? "_iType" : "_iSubType"));
        return mode == 1 ? !listed : listed;
    }

    private sealed class CastCosts
    {
        public Dictionary<int, long> Attrs { get; } = new();
        public List<(int Entry, long Count)> Items { get; } = new();
        public Dictionary<int, long> Buffs { get; } = new();
        public Dictionary<int, long> Energy { get; } = new();
        public bool Invalid { get; set; }
    }

    private CastCosts ResolveCosts(GameSession s, JsonElement row)
    {
        var result = new CastCosts();
        var stats = character.Stats(s);
        foreach (var tuple in Tuples(row, "_kCost"))
        {
            var c = tuple.EnumerateArray().Select(v => (long)(GameData.Number(v) ?? 0)).ToArray();
            // (inferred) retained opaque type-4 markers follow the live client checker, which skips them.
            // Lua_GameDefine.enum_SkillCostType / LC_Skill_Function.CheckSkillCost only consume 1/2/3/6/7/8.
            if (c.Length == 2 && c[0] == 4) continue;
            if (c.Length < 3) { result.Invalid = true; continue; }
            int key = (int)c[1];
            long amount = c.Length > 3 ? c[3] : c[2];
            switch (c[0])
            {
                case 1 when c.Length >= 4 && key is 2000 or 2001:
                    amount += stats[key == 2000 ? 101 : 102] * c[2] / 10000;
                    result.Attrs[key] = result.Attrs.GetValueOrDefault(key) + Math.Max(0, amount);
                    break;
                case 7 when c.Length >= 4 && key is 2000 or 2001:
                    // AttrConvert [7, resource, basis-property, ratio], LC_Skill_Function cost checker.
                    result.Attrs[key] = result.Attrs.GetValueOrDefault(key) + Math.Max(0, stats[(int)c[2]] * c[3] / 10000);
                    break;
                case 2: result.Items.Add((key, Math.Max(0, c[2]))); break;
                case 3: result.Buffs[key] = result.Buffs.GetValueOrDefault(key) + Math.Max(0, c[2]); break;
                case 8: result.Energy[key] = result.Energy.GetValueOrDefault(key) + Math.Max(0, amount); break;
                default: result.Invalid = true; break; // reject an unsupported cost rather than grant a free cast.
            }
        }
        return result;
    }

    private (int Error, long Parameter) ValidateCosts(GameSession s, CastCosts costs)
    {
        if (costs.Invalid) return (ErrorCodes.SkillRequirements, 0);
        var stats = character.Stats(s);
        foreach (var (attr, cost) in costs.Attrs)
            if (cost > stats[attr] || attr == 2000 && cost >= stats[attr]) return (ErrorCodes.SkillCostShort, attr);
        foreach (var (group, count) in costs.Buffs)
            if (!buffs.HasBuffPiles(s, group, (int)count))
                return (ErrorCodes.SkillBuffCostShort, group);
        return (0, 0);
    }

    private async Task PayCostsAsync(GameSession s, CastCosts costs)
    {
        if (costs.Attrs.Count > 0)
        {
            var stats = character.Stats(s);
            await character.SetHpSpAsync(s, stats[2000] - costs.Attrs.GetValueOrDefault(2000), stats[2001] - costs.Attrs.GetValueOrDefault(2001));
        }
        foreach (var (group, count) in costs.Buffs)
            await buffs.ConsumeBuffPilesAsync(s, group, (int)count);
    }

    private async Task StartCooldownAsync(GameSession s, JsonElement row)
    {
        int group = (int)GameData.Int(row, "_iSkillID"), mode = (int)GameData.Int(row, "_iMode");
        long now = Now, cd = GameData.Int(row, "_iCD");
        var notice = new SkillCdNotify { Uuid = s.Role!.Id };
        void Set(int g, int m, long duration, bool shared)
        {
            if (duration <= 0) return;
            long end = now + duration;
            s.SkillModeCds[(g, m)] = Math.Max(end, s.SkillModeCds.GetValueOrDefault((g, m)));
            if (shared) s.SkillCds[g] = Math.Max(end, s.SkillCds.GetValueOrDefault(g));
            notice.Infos.Add(new SkillCdInfo { SkillId = g, SkillMode = m, CdStartTimestampMs = now, CdEndTimestampMs = end });
        }
        Set(group, mode, cd, GameData.Int(row, "_iCdModeGroup") < 0);
        var shared = Numbers(row, "_iPubCD");
        var groups = Numbers(row, "_iPubCDGroup");
        // Parallel arrays, e.g. BackSlide [15104,15330,15329] / [1000,15000,0] (inferred server application).
        for (int i = 0; i < Math.Min(shared.Length, groups.Length); i++)
        {
            int sibling = (int)groups[i];
            if (data.Row("SkillConfig", sibling) is { } siblingRow) sibling = (int)GameData.Int(siblingRow, "_iSkillID");
            if (sibling > 0) Set(sibling, 0, (long)shared[i], true);
        }
        if (notice.Infos.Count > 0) await s.SendGameAsync(SkillCdUpdate, notice);
    }

    /// <summary>Legacy auto-attack entry point, using the same cancellation-safe cast owner as manual skills.</summary>
    private async Task SwingAsync(GameSession s, MonsterUnit? target, int skillId, long id)
    {
        if (s.Role == null || s.Progress == null) return;
        using var held = await CharacterService.Lock(s);
        if (target != null && !CanHitMonster(s, target)) return;
        if (EffectiveSkillRow(s, skillId) is not { } row || !Learned(s, row) || !SkillModeAllowed(s, row)
            || buffs.HasForbidden(s, 5) || s.SkillCast is { Cancelled: false }) return;
        var request = new CastSkillReq { SkillId = (int)GameData.Int(row, "_iID"), TargetId = target?.Uid ?? 0,
            TargetPos = target?.Pos ?? s.Position.Clone(), Face = s.Face };
        if (!await AdmitAutomaticCostAsync(s, row, request, id)) { s.Attack = null; return; }
        if (GameData.Int(row, "_iIsNormalAtk") == 1)
            s.NextNormalAttackMs = Now + SkillMs(s, row);
        await StartCastAsync(s, row, request, id);
    }

    private async Task<bool> AdmitAutomaticCostAsync(GameSession s, JsonElement row, CastSkillReq request, long id)
    {
        var costs = ResolveCosts(s, row);
        if (costs.Items.Count == 0) return true;
        if (ValidateCosts(s, costs).Error != 0 || !buffs.CanSpendEnergy(s, row)) return false;
        var lease = s.Attachment;
        long scene = s.SceneGeneration, worldScene = s.WorldScene;
        bool Current() => lease?.Cancelled != true && ReferenceEquals(s.Attachment, lease) &&
            s.SceneGeneration == scene && s.WorldScene == worldScene && !s.SceneLoading && !s.Dead;
        if (!await AdmitCastCostAsync(s, row, request, costs, id) || !Current()) return false;
        await PayCostsAsync(s, costs);
        await buffs.SpendEnergyAsync(s, row);
        return Current();
    }

    private async Task StartCastAsync(GameSession s, JsonElement row, CastSkillReq req, long id, SkillObject? actor = null)
    {
        var origin = actor?.Pos ?? s.Position.Clone();
        var target = FindMonster(s.WorldScene, req.TargetId);
        float face = target != null ? MonsterService.Angle(origin.X, origin.Z, target.X, target.Z) : req.Face;
        var action = new ActiveSkillCast
        {
            Id = id, OwnerId = s.Role!.Id, WorldScene = s.WorldScene, SceneGeneration = s.SceneGeneration,
            Attachment = s.Attachment, Request = req, Row = row, Origin = origin, StartedWalking = actor == null && s.Walking,
            StartMs = Now, ChantMs = (int)Math.Max(0, GameData.Int(row, "_iChantFix") + GameData.Int(row, "_iChantVar")), ActionMs = SkillMs(s, row), Operator = actor,
        };
        s.SkillCast = action;
        s.WorldState.ResurrectionCastId = Script(row) == "Resurrection" ? id : 0;
        s.Face = face;
        req.Face = face;
        await SendCastTimelineAsync(s, new UseInSkillInfo
        {
            SrcID = action.SourceId, SkillID = req.SkillId, TargetId = req.TargetId, TargetPos = req.TargetPos ?? origin,
            Face = face, SessionId = id, ChantMs = action.ChantMs, SkillMs = action.ActionMs, StartTime = action.StartMs, PlayAct = true,
        }, highLevel: GameData.Int(row, "_iSkillEffectLevel") > 0, extra: actor != null || req.IsExtraSkill);
        var template = data.SkillTemplate(GameData.Int(row, "_iScriptShow"));
        var points = template is { } t ? Numbers(t, "m_kAtkPoints") : [];
        double point = points.FirstOrDefault(0.2);
        int at = Script(row) == "Resurrection" ? action.ChantMs : ImpactOffset(template, point, action.ChantMs, action.ActionMs);
        ScheduleRuntime(action.WorldScene, TimeSpan.FromMilliseconds(at), async () =>
        {
            if (!Live(s, action)) return;
            action.Resolved = true;
            await ResolveCastAsync(s, action);
        });
        ScheduleRuntime(action.WorldScene, TimeSpan.FromMilliseconds(Math.Max(at, action.ChantMs + action.ActionMs) + 1), () =>
        {
            if (ReferenceEquals(s.SkillCast, action)) s.SkillCast = null;
            return Task.CompletedTask;
        });
    }

    /// <summary>Piecewise template time mapping; same scale as LC_Skill_RunTimeContext (inferred for attack points).</summary>
    internal static int ImpactOffset(JsonElement? template, double point, int chantMs, int actionMs)
    {
        double chant = 0, end = 1;
        if (template is { } t && t.TryGetProperty("m_kState", out var state))
        {
            var times = Numbers(state, "Time");
            if (times.Length >= 3) { chant = times[1]; end = times[2]; }
        }
        double ms = point < chant && chant > 0 ? point / chant * chantMs : chantMs + Math.Clamp((point - chant) / Math.Max(0.001, end - chant), 0, 1) * actionMs;
        return (int)Math.Max(0, Math.Round(ms));
    }

    private static bool Live(GameSession s, ActiveSkillCast action) => !action.Cancelled && ReferenceEquals(s.SkillCast, action)
        && !s.Dead && !s.SceneLoading && s.Role?.Id == action.OwnerId && s.WorldScene == action.WorldScene
        && (action.Operator == null || !action.Operator.Removing && action.Operator.ExpiresMs > Now);

    private int SkillMs(GameSession s, JsonElement row)
    {
        double aspd = Math.Clamp(character.Stats(s)[115] / 100.0, 0.1, 5);
        double influence = GameData.Int(row, "_iActionTimeInfluence") / 100.0;
        return (int)Math.Round(Math.Max(1, GameData.Int(row, "_iActionTime")) / Math.Max(0.1, 1 + (aspd - 1) * influence));
    }

    public async Task InterruptAsync(GameSession s, SkillInterruptReq? req = null)
    {
        bool valid = req == null || req.InterruptSkillId == 0 || req.InterruptSkillId == s.SkillCast?.Request.SkillId
            || req.InterruptSkillId == s.Attack?.SkillId || eventAttacks.TryGetValue(s, out var eventAttack) && req.InterruptSkillId == eventAttack.SkillId;
        bool interrupted = valid && (s.SkillCast != null || s.Attack != null || eventAttacks.ContainsKey(s));
        if (interrupted)
        {
            s.Attack = null;
            eventAttacks.Remove(s);
            await CancelCastAsync(s, req?.NxtSkill != null ? SkillInterruptedInfo.Types.SkillInterruptType.Skill : SkillInterruptedInfo.Types.SkillInterruptType.Move);
        }
        await s.SendGameAsync(SkillInterrupt, new SkillInterruptResp { IsInterruptSucc = interrupted });
        if (interrupted && req?.NxtSkill is { } next) await CastAsync(s, next);
    }

    private async Task CancelCastAsync(GameSession s, SkillInterruptedInfo.Types.SkillInterruptType cause)
    {
        s.WorldState.ResurrectionCastId = 0;
        if (s.SkillCast is not { } action) return;
        action.Cancelled = true;
        s.SkillCast = null;
        await ToPlayerAndViewersAsync(s, SkillEnd, new SkillEndNotify
        {
            SrcID = action.SourceId, SessionId = action.Id, Type = SkillEndNotify.Types.SkillEndType.Interrupted,
            ClearEffect = true, InterruptedInfo = new SkillInterruptedInfo { Type = cause },
        });
    }

    public void PlayerMoved(GameSession s)
    {
        if (eventAttacks.TryGetValue(s, out var eventAttack)
            && MonsterService.Distance(eventAttack.StartX, eventAttack.StartZ, s.Position.X, s.Position.Z) > 1.5f) eventAttacks.Remove(s);
        if (s.Attack is { } a && MonsterService.Distance(a.StartX, a.StartZ, s.Position.X, s.Position.Z) > 0.1f) s.Attack = null;
        if (s.SkillCast is not { } cast || Distance(cast.Origin, s.Position) <= 0.1f || GameData.Int(cast.Row, "_bMovement") != 0) return;
        // The client casts on reaching range and stops 1 ms later (in-game 11.10): that first stop is the cast's real
        // origin, within the distance the walk can have covered, not a move that interrupts it.
        if (cast.StartedWalking && !s.Walking && Now - cast.StartMs <= 300
            && Distance(cast.Origin, s.Position) <= s.Speed / 100f * 0.75f + 0.5f)
        {
            cast.Origin = s.Position.Clone();
            cast.StartedWalking = false;
            return;
        }
        loop.TrackRuntimeDetached("cast movement cancellation", () => CancelCastAsync(s, SkillInterruptedInfo.Types.SkillInterruptType.Move));
    }
}
