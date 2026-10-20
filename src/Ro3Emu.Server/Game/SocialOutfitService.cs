using System.Text.Json;
using Google.Protobuf;
using Romsg;
using Ro3Emu.Server.Accounts;
using Ro3Emu.Server.Data;

namespace Ro3Emu.Server.Game;

/// <summary>
/// Social outfit (head icon, head frame, chat bubble, title, honor, card; SocialOutfitConfig): AUTO_SYNC 5044 DBSocialOutfit,
/// 83480 wear and 83481 refresh. LC_DAL_SocialOutfit._allDBSocialOutFitInfoProcess (lua-src Logic/Asset/LC_DAL_SocialOutfit.lua:148-347)
/// builds SocialOutfitEquipData from socialOutfitEquip, SocialUnlockInfo from socialOutfitContainer and SocialLimitedTimeInfo
/// from each time-limited entry's expireTimestamp (unix seconds: the wear panels count down to it with GetSystemTime,
/// UI_Sys_Chat_Wear_WidgetBaseCtrl._refreshView), each only when the map is present. The profile card reads
/// SocialOutfitEquipData[ChangeToTitle / ChangeToHonor] for the own role without a nil check
/// (UI_Sys_Personal_InfoWidgetCtrl._refreshPlayerBaseInfoShow), so 5044 is always sent.
/// <para>(inferred) Defaults: worn, the _iIsDefaultWear row of each type, for the head the one of the role's career and
/// sex (the Novice's when the career has none); owned permanently, the _iIsDefaultUnLock rows of the role's sex. They are
/// derived from the configuration each time. Stored (role blob "social_outfit"): the outfits items unlocked (action 349)
/// and the worn choices (83480), laid over the defaults. A worn choice no longer owned falls back to the default.</para>
/// <para>Other players read the worn outfits as RoleAdditionBaseInfo.socialOutfitData (UserDataService "Base",
/// LC_DAL_RolePublic.ParseRolePublicBaseInfo), which the chat bubble, honor and the profile card index unguarded.</para>
/// </summary>
public sealed class SocialOutfitService
{
    public const uint Sync = 5044, Equip = 83480, Refresh = 83481;
    internal const int UnlockAction = 349;
    // enum_SocialOutfitType (Lua_ChatDefine.lua): 1 head, 2 head frame, 3 bubble, 4 title, 5 honor, 6 card, 7 theme.
    private const int Head = 1, NoviceJob = 1000;
    // PBSocialOutfitStatus: 0 lock, 1 condition, 2 time limit, 3 permanent.
    private const int TimeLimit = 2, Permanent = 3;
    // ErrorCodeConfig 7056 "This cosmetic is already unlocked and cannot be obtained again through Repeat acquisition."
    // and 174 "Gender mismatch".
    public const int ErrAlreadyUnlocked = 7056, ErrSex = 174;
    private const string ExpiryKey = "social-outfit-expiry";

    private readonly GameData data;
    private readonly RoleStore roles;
    private readonly CharacterService character;
    private readonly Deadlines deadlines;
    private readonly TimeProvider clock;

    public SocialOutfitService(GameData data, RoleStore roles, CharacterService character, Deadlines deadlines, TimeProvider clock)
    {
        this.data = data; this.roles = roles; this.character = character; this.deadlines = deadlines; this.clock = clock;
        character.JobChanged.Add(async s =>
        {
            if (s.SocialOutfit != null && s.Role != null)
            {
                int career = s.Progress?.Career ?? s.Role.Career;
                if (s.SocialOutfit.SocialOutfitEquip.TryGetValue(Head, out var eq) && data.Row("SocialOutfitConfig", eq.CfgId) is { } row)
                {
                    long job = GameData.Int(row, "_iJobID");
                    if (job > 0 && job != career)
                    {
                        var view = View(data, s.Role.Sex, career, s.SocialOutfit, Now);
                        if (view.SocialOutfitEquip.TryGetValue(Head, out var curHead))
                        {
                            s.SocialOutfit.SocialOutfitEquip[Head] = new DBSocialOutfitEquip { CfgId = curHead.CfgId };
                            await CommitAsync(s, s.SocialOutfit, "job_change_head");
                        }
                    }
                }
            }
            if (s.Synced.ContainsKey(Sync)) await SyncAsync(s);
        });
        character.RegisterItemAction(UnlockAction, UseUnlockItemAsync);
    }

    private long Now => clock.GetUtcNow().ToUnixTimeSeconds();

    public async Task LoginAsync(GameSession s)
    {
        var bytes = roles.LoadSocialOutfit(s.Role!.Id);
        s.SocialOutfit = bytes is { Length: > 0 } ? DBSocialOutfit.Parser.ParseFrom(bytes) : new DBSocialOutfit();
        await PruneAsync(s);
        int career = s.Progress?.Career ?? s.Role!.Career;
        if (s.SocialOutfit.SocialOutfitEquip.TryGetValue(Head, out var eq) && data.Row("SocialOutfitConfig", eq.CfgId) is { } row)
        {
            long job = GameData.Int(row, "_iJobID");
            if (job > 0 && job != career)
            {
                var view = View(data, s.Role.Sex, career, s.SocialOutfit, Now);
                if (view.SocialOutfitEquip.TryGetValue(Head, out var curHead))
                {
                    s.SocialOutfit.SocialOutfitEquip[Head] = new DBSocialOutfitEquip { CfgId = curHead.CfgId };
                    await CommitAsync(s, s.SocialOutfit, "career_head_update");
                }
            }
        }
        await SyncAsync(s);
    }

    private Task SyncAsync(GameSession s)
    {
        ArmExpiry(s);
        return CharacterService.SyncAsync(s, Sync, View(data, s.Role!.Sex, s.Progress?.Career ?? s.Role.Career, s.SocialOutfit, Now));
    }

    /// <summary>The 5044 the client sees: the defaults, then the stored unlocks still in time and the worn choices.</summary>
    internal static DBSocialOutfit View(GameData data, int sex, int career, DBSocialOutfit? stored, long now)
    {
        var info = Build(data, sex, career);
        foreach (var (type, container) in stored?.SocialOutfitContainer ?? [])
            foreach (var (id, owned) in container.SocialOutfitInfos)
            {
                if (owned.Status == TimeLimit && owned.ExpireTimestamp <= now) continue;
                if (!info.SocialOutfitContainer.TryGetValue(type, out var into))
                    info.SocialOutfitContainer[type] = into = new DBSocialOutfitContainer();
                if (into.SocialOutfitInfos.TryGetValue(id, out var have) && have.Status == Permanent) continue;
                into.SocialOutfitInfos[id] = owned.Clone();
            }
        foreach (var (type, equip) in stored?.SocialOutfitEquip ?? [])
            if (info.SocialOutfitContainer.TryGetValue(type, out var container) && container.SocialOutfitInfos.ContainsKey(equip.CfgId))
            {
                if (type == Head && data.Row("SocialOutfitConfig", equip.CfgId) is { } row)
                {
                    long job = GameData.Int(row, "_iJobID");
                    if (job > 0 && job != career)
                    {
                        // Stored was a class head from an old job (e.g. Novice 1000 vs current Lord Knight 1300).
                        // Keep info.SocialOutfitEquip[type], which Build already set to current career head.
                        continue;
                    }
                }
                info.SocialOutfitEquip[type] = new DBSocialOutfitEquip { CfgId = equip.CfgId };
            }
        return info;
    }

    /// <summary>Slot type to worn outfit, for another player's RoleAdditionBaseInfo.socialOutfitData.</summary>
    internal static SocialOutfitData Worn(GameData data, int sex, int career, DBSocialOutfit? stored, long now)
    {
        var worn = new SocialOutfitData();
        foreach (var (type, equip) in View(data, sex, career, stored, now).SocialOutfitEquip) worn.SocialOutfit[type] = equip.CfgId;
        return worn;
    }

    private static DBSocialOutfit Build(GameData data, int sex, int career)
    {
        var rows = data.Table("SocialOutfitConfig").Values.ToList();
        bool SexOf(JsonElement r) => GameData.Int(r, "_iSex") is var rowSex && (rowSex == 0 || rowSex == sex);
        var info = new DBSocialOutfit();
        foreach (var group in rows.Where(r => GameData.Int(r, "_iIsDefaultWear") == 1 && SexOf(r)).GroupBy(r => (int)GameData.Int(r, "_iType")))
        {
            var worn = group.Key != Head ? group.First()
                : group.FirstOrDefault(r => GameData.Int(r, "_iJobID") == career) is { ValueKind: JsonValueKind.Object } own ? own
                : group.FirstOrDefault(r => GameData.Int(r, "_iJobID") == NoviceJob);
            if (worn.ValueKind == JsonValueKind.Object) info.SocialOutfitEquip[group.Key] = new DBSocialOutfitEquip { CfgId = GameData.Int(worn, "_iID") };
        }
        var owned = rows.Where(r => GameData.Int(r, "_iIsDefaultUnLock") == 1 && SexOf(r))
            .Select(r => ((int)GameData.Int(r, "_iType"), GameData.Int(r, "_iID")))
            .Concat(info.SocialOutfitEquip.Select(e => (e.Key, e.Value.CfgId))).Distinct();
        foreach (var (type, id) in owned)
        {
            if (!info.SocialOutfitContainer.TryGetValue(type, out var container))
                info.SocialOutfitContainer[type] = container = new DBSocialOutfitContainer();
            container.SocialOutfitInfos[id] = new DBSocialOutfitInfo { CfgId = id, Status = Permanent };
        }
        return info;
    }

    // ------------------------------------------------------------------ 349 item unlock

    /// <summary>
    /// 349 UnlockSocialOutfit [349, SocialOutfitWearConfig id]: the wear row names the outfit (_iSocialOutfit) and its
    /// duration (_iTimeLimit seconds, -1 permanent: 50015/50019 are the two 24 h honors). An item may list one row per sex
    /// (1910003 Composer [[349,10003],[349,10004]], heads 100003 male and 100004 female): only the role's sex applies, and
    /// an item with none for it is ErrSex. The client checks nothing before 31323 and has no notice of its own: the 5044
    /// container diff raises "Successfully activated" (72015, LC_DAL_SocialOutfit :350-511). (inferred) An outfit already
    /// owned for good is refused with ErrAlreadyUnlocked, keeping the item; a timed one used again extends from its
    /// current end, and a permanent one replaces a timed one.
    /// </summary>
    private async Task<int> UseUnlockItemAsync(ItemActionUse use)
    {
        var s = use.Session;
        if (s.SocialOutfit == null || s.Role == null) return ErrorCodes.FeatureUnavailable;
        if (use.Count != 1) return ErrorCodes.Parameter;
        var picks = new List<(int Type, long Id, long Seconds)>();
        bool anyRow = false;
        foreach (var effect in use.Effects)
        {
            if (effect.Length < 2 || data.Row("SocialOutfitWearConfig", effect[1]) is not { } wear
                || data.Row("SocialOutfitConfig", GameData.Int(wear, "_iSocialOutfit")) is not { } outfit) return ErrorCodes.ItemNoConfig;
            anyRow = true;
            if (GameData.Int(outfit, "_iSex") is var sex and not 0 && sex != s.Role.Sex) continue;
            picks.Add(((int)GameData.Int(outfit, "_iType"), GameData.Int(outfit, "_iID"), GameData.Int(wear, "_iTimeLimit")));
        }
        if (!anyRow) return ErrorCodes.ItemNoConfig;
        if (picks.Count == 0) return ErrSex;
        long now = Now;
        var view = View(data, s.Role.Sex, s.Progress?.Career ?? s.Role.Career, s.SocialOutfit, now);
        bool Permanently(int type, long id) => view.SocialOutfitContainer.TryGetValue(type, out var c)
            && c.SocialOutfitInfos.TryGetValue(id, out var o) && o.Status == Permanent;
        if (picks.All(p => Permanently(p.Type, p.Id))) return ErrAlreadyUnlocked;
        var next = s.SocialOutfit.Clone();
        foreach (var (type, id, seconds) in picks.Where(p => !Permanently(p.Type, p.Id)))
        {
            if (!next.SocialOutfitContainer.TryGetValue(type, out var container))
                next.SocialOutfitContainer[type] = container = new DBSocialOutfitContainer();
            var current = container.SocialOutfitInfos.GetValueOrDefault(id);
            bool live = current is { Status: TimeLimit } && current.ExpireTimestamp > now;
            container.SocialOutfitInfos[id] = seconds < 0
                ? new DBSocialOutfitInfo { CfgId = id, Status = Permanent, UnlockTimestamp = now }
                : new DBSocialOutfitInfo { CfgId = id, Status = TimeLimit, UnlockTimestamp = live ? current!.UnlockTimestamp : now,
                    ExpireTimestamp = (live ? current!.ExpireTimestamp : now) + seconds };
        }
        byte[] saved = next.ToByteArray();
        if (!await character.CommitItemActionAsync(use, tx => tx.RoleBlob(s.Role.Id, "social_outfit", saved), () => s.SocialOutfit = next,
            new { SocialOutfit = picks.Select(p => new[] { p.Type, p.Id, p.Seconds }).ToArray() })) return ErrorCodes.ItemsShort;
        await SyncAsync(s);
        return 0;
    }

    // ------------------------------------------------------------------ 83480 / 83481

    /// <summary>
    /// 83480 EquipSocialOutfitReq {cfgId, type}: the wear panels (UI_Sys_Chat_Wear_WidgetBaseCtrl._onButtonClick, the
    /// profile editor's bottom bar) send it for an unlocked outfit only (72007 "Outfit Not Yet Unlocked" otherwise). The
    /// reply handler is empty (LC_DAL_SocialOutfit._processEquip); the worn state reaches the client through the 5044
    /// socialOutfitEquip diff. (inferred) An outfit not owned, of another type or of the other sex is Parameter.
    /// </summary>
    public async Task EquipAsync(GameSession s, EquipSocialOutfitReq req)
    {
        using var held = await CharacterService.Lock(s);
        int err = s.SocialOutfit == null || s.Role == null ? ErrorCodes.FeatureUnavailable : 0;
        if (err == 0)
        {
            await PruneAsync(s);
            var view = View(data, s.Role!.Sex, s.Progress?.Career ?? s.Role.Career, s.SocialOutfit, Now);
            int type = (int)req.Type;
            if (data.Row("SocialOutfitConfig", req.CfgId) is not { } row || GameData.Int(row, "_iType") != type
                || !view.SocialOutfitContainer.TryGetValue(type, out var owned) || !owned.SocialOutfitInfos.ContainsKey(req.CfgId))
                err = ErrorCodes.Parameter;
            else if (view.SocialOutfitEquip.GetValueOrDefault(type)?.CfgId != req.CfgId)
            {
                var next = s.SocialOutfit!.Clone();
                next.SocialOutfitEquip[type] = new DBSocialOutfitEquip { CfgId = req.CfgId };
                await CommitAsync(s, next, "equip");
            }
        }
        await s.SendGameAsync(Equip, new EquipSocialOutfitRes { Err = err });
        if (err == 0) await SyncAsync(s);
    }

    /// <summary>83481 RefreshSocialOutfitReq: drops what ran out and sends 5044 again (no reply handler in the client).</summary>
    public async Task RefreshAsync(GameSession s)
    {
        using var held = await CharacterService.Lock(s);
        int err = s.SocialOutfit == null ? ErrorCodes.FeatureUnavailable : 0;
        if (err == 0) await PruneAsync(s);
        await s.SendGameAsync(Refresh, new RefreshSocialOutfitRes { Err = err });
        if (err == 0) await SyncAsync(s);
    }

    // ------------------------------------------------------------------ expiry

    /// <summary>
    /// The client never drops a timed outfit by itself: the server removes it when it runs out and, if it was worn, the
    /// slot falls back to the default (View). A removable deadline at the earliest end, as MountService arms its own.
    /// </summary>
    private void ArmExpiry(GameSession s)
    {
        long next = s.SocialOutfit?.SocialOutfitContainer.Values.SelectMany(c => c.SocialOutfitInfos.Values)
            .Where(o => o.Status == TimeLimit).Select(o => o.ExpireTimestamp).DefaultIfEmpty().Min() ?? 0;
        if (next == 0) deadlines.Cancel(s, ExpiryKey);
        else deadlines.Set(s, ExpiryKey, DateTimeOffset.FromUnixTimeSeconds(next), (session, due, lease, ct) => ExpireAsync(session, lease));
    }

    private async Task ExpireAsync(GameSession s, DeadlineLease lease)
    {
        using var held = await CharacterService.Lock(s);
        if (!deadlines.IsCurrent(s, lease) || s.SocialOutfit == null) return;
        await PruneAsync(s);
        await SyncAsync(s);
    }

    /// <summary>Removes the stored timed outfits that ran out, and a worn choice of one; the caller holds StateLock.</summary>
    private async Task PruneAsync(GameSession s)
    {
        long now = Now;
        var next = s.SocialOutfit!.Clone();
        bool changed = false;
        foreach (var (type, container) in next.SocialOutfitContainer.ToArray())
        {
            foreach (var (id, owned) in container.SocialOutfitInfos.ToArray())
            {
                if (owned.Status != TimeLimit || owned.ExpireTimestamp > now) continue;
                container.SocialOutfitInfos.Remove(id);
                if (next.SocialOutfitEquip.GetValueOrDefault(type)?.CfgId == id) next.SocialOutfitEquip.Remove(type);
                changed = true;
            }
            if (container.SocialOutfitInfos.Count == 0) next.SocialOutfitContainer.Remove(type);
        }
        if (changed) await CommitAsync(s, next, "expire");
    }

    private async Task CommitAsync(GameSession s, DBSocialOutfit next, string action)
    {
        var transaction = roles.NewTransaction();
        transaction.RoleBlob(s.Role!.Id, "social_outfit", next.ToByteArray());
        await roles.CommitAsync(transaction, "social-outfit/" + action, _ => s.SocialOutfit = next);
    }
}
