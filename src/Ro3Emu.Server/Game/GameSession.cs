using System.Buffers;
using Google.Protobuf;
using MessagePack;
using Phonest.Proto;
using Ro3Emu.Server.Accounts;
using Ro3Emu.Server.Net;

namespace Ro3Emu.Server.Game;

/// <summary>
/// One client on the game gate. Server -> client traffic is Phonest EntityMessage on gate index 2:
/// named RPCs (methodName + msgpack values concatenated without an array header) or raw game messages
/// (no methodName, parameters = uint32 LE msgId ‖ romsg protobuf).
/// </summary>
public sealed class GameSession(PhonestChannel channel, TrafficLog traffic)
{
    /// <summary>The current game transport. A silent reconnect (ReconnectService) swaps it on the loop; frames and
    /// closes of a swapped-out channel are ignored by the gate.</summary>
    public PhonestChannel Channel { get; internal set; } = channel;

    /// <summary>The socket dropped and the role waits in its reconnect grace.</summary>
    internal bool TransportLost { get; set; }

    /// <summary>The server closed this connection to force a full relogin (recovery): no reconnect grace.</summary>
    internal bool ReconnectBlocked { get; set; }

    /// <summary>A reconnect found a 70010 handoff pending: the client's 30005 publication sends a 31037 even on a map
    /// without NPCs, since the client's socket resync waits for one (spec §1.6.5).</summary>
    internal bool ReconnectAoiPending { get; set; }

    /// <summary>Closes the connection for a recovery relogin; unlike a network drop it never waits for a reconnect.</summary>
    internal void AbortForRecovery()
    {
        ReconnectBlocked = true;
        Channel.Abort();
    }

    public long HandshakeToken { get; set; }

    /// <summary>The signed-in account; set only by <see cref="SessionRegistry.SignInAsync"/> (tests may set it).</summary>
    public string? Account { get; internal set; }
    internal long AccountGeneration { get; set; }
    internal Task PredecessorFence { get; set; } = Task.CompletedTask;

    public Role? Role { get; set; }
    public Ownership.OwnerRegistry? Owners { get; internal set; }
    public Ownership.RoleOwner? Owner { get; internal set; }
    public Ownership.AttachmentLease? Attachment { get; internal set; }
    // Cleanup may run after a selection bound a role but before its per-role loaders finished.
    internal bool RolePositionReady { get; set; }
    internal bool GuildOnlinePublished { get; set; }

    internal void ClearRoleLoadState()
    {
        RolePositionReady = false;
        GuildOnlinePublished = false;
        Progress = null;
        Daily = null; DailyRows = null; DailyReady = false;
        WildAfk = null; Guild = null; Tasks = null;
        Achievements = null; AchievementRuntime = null; AdventureGroupRole = null;
        Library = null; CardLibraryReadyAttachment = null; GrowthClass = null;
        NewActivities = null; ActivitiesReady = false;
        Dungeons = null; DungeonAttempt = null;
        Tower = null; TowerAttempt = null;
        Training = null; EventFaction = 0;
        TerritoryWars = null; TerritoryBattle = null; SiegeBattle = null; TerritoryEscort = null;
        Defense = null; TimedBattle = null;
        LifeJob = null; Restrictions = null;
        Items = new(); Pets = null;
        WorldState = new();
        Power = null; PowerPublishedMs = 0;
    }
    private CancellationTokenSource _pendingCharacterIntents = new();
    internal CancellationToken PendingCharacterIntent => _pendingCharacterIntents.Token;
    // Only speculative, not-yet-locked grants use this pulse. Admitted mutations keep their durability.
    internal void CancelPendingCharacterIntents()
    {
        var previous = _pendingCharacterIntents;
        _pendingCharacterIntents = new();
        previous.Cancel(); previous.Dispose();
    }

    public WorldSessionState WorldState { get; set; } = new();

    /// <summary>One map transition per selected session, including asynchronous leave hooks.</summary>
    public SemaphoreSlim SceneTransitionGate { get; } = new(1, 1);

    /// <summary>Scene entry the client was sent to (70010) and is expected to report loaded (30005).</summary>
    public long SceneEntry { get; set; }

    /// <summary>Allocated dungeon instance, or zero for the shared world map. Never a config id.</summary>
    public long InstanceId { get; set; }

    /// <summary>Simulation/AOI key; the wire scene entry and navigation still use SceneEntry.</summary>
    public int LineNumber { get; set; } = 1;
    public Ro3Emu.Server.Maps.RuntimeMapKind RuntimeKind { get; set; }
    public Ro3Emu.Server.Maps.RuntimeMapKey RuntimeMap => InstanceId == 0
        ? Ro3Emu.Server.Maps.RuntimeMapKey.Public(SceneEntry, LineNumber)
        : RuntimeKind == Ro3Emu.Server.Maps.RuntimeMapKind.Event
            ? Ro3Emu.Server.Maps.RuntimeMapKey.Event(SceneEntry, InstanceId)
            : Ro3Emu.Server.Maps.RuntimeMapKey.Private(SceneEntry, InstanceId);
    public long WorldScene => RuntimeMap.WorldScene;

    /// <summary>One outstanding 70010/30005 handoff. Generation fences continuations; the wire has no nonce.</summary>
    public long SceneGeneration { get; set; }
    public SceneHandoff? Handoff { get; set; }
    public bool SceneRecoveryRequired { get; set; }

    /// <summary>Last position the client reported (WALK is client-authoritative), and the facing in degrees.</summary>
    public Romsg.Vec3 Position { get; set; } = new();

    /// <summary>Position before the latest WALK, to tell walks from jumps.</summary>
    public Romsg.Vec3 PreviousPosition { get; set; } = new();

    public float Face { get; set; }

    /// <summary>Speed attribute (idx 113) of the role; the client moves at Speed/100 units per second.</summary>
    public long Speed { get; set; } = RoleSessionService.DefaultSpeed;

    public DateTime PositionSavedUtc { get; set; }
    /// <summary>C/D 18 lazy public-position state (PositionPersistence).</summary>
    internal Persistence.LazyRolePart.State? PositionPart { get; set; }
    /// <summary>C/D 17: the role's client settings, merged in memory and written lazily (SettingsPersistence).</summary>
    internal Romsg.DBSettingInfo? Settings { get; set; }
    internal Persistence.LazyRolePart.State? SettingsPart { get; set; }

    /// <summary>Dead (HP 0, 31049 state 1) until revived (31013).</summary>
    public bool Dead { get; set; }
    /// <summary>One process run and dead-to-alive cycle identify retries of the same paid revival.</summary>
    internal string? ReviveDeathSource { get; set; }

    /// <summary>The 31029 lock target and the running auto-attack (CombatService).</summary>
    public long LockTarget { get; set; }
    public AttackLoop? Attack { get; set; }
    /// <summary>Next timestamp (server ms) when a normal attack can start (governed by ASPD).</summary>
    public long NextNormalAttackMs { get; set; }
    public bool NoCd { get; set; }

    /// <summary>Authoritative cast generation; delayed effects cannot outlive an interrupted/replaced cast.</summary>
    public ActiveSkillCast? SkillCast { get; set; }
    public SemaphoreSlim SkillCastGate { get; } = new(1, 1);
    public List<SkillObject> SkillObjects { get; } = new();
    public Dictionary<(int Group, int Mode), long> SkillModeCds { get; } = new();
    public CombatService.ProtocolState SkillProtocol { get; set; } = new();

    /// <summary>Status bits (property 2701, bit 15 = fighting) and when the player last dealt or took a hit.</summary>
    public long StatusBits { get; set; }
    public long LastCombatMs { get; set; }

    /// <summary>Active buffs (BuffService) and cooldown ends in server ms: skills by group id, items by id, item share groups.</summary>
    public List<ActiveBuff> Buffs { get; } = new();
    /// <summary>Buff-owned portions of 2701/2702/2711; 2701 also contains CombatService's fighting bit.</summary>
    public ulong BuffStatusBits { get; set; }
    public ulong ForbiddenBits { get; set; }
    public ulong StaggerBits { get; set; }
    public HashSet<int> ForbiddenOrdinals { get; } = new();
    /// <summary>Transient configured energy meters; BuffService sends each change as 31334.</summary>
    public Dictionary<int, PlayerEnergyState> Energy { get; } = new();
    public int EnergyCareer { get; set; }
    /// <summary>EnergyConfig._iBelongMount mounts owned when the meters were last reconciled (BuffService.Energy).</summary>
    public int EnergyMounts { get; set; }
    /// <summary>31134/31135 lethal-hit choice; only an eligible active Osiris buff may create it.</summary>
    public ImmuneDeathOffer? ImmuneDeathOffer { get; set; }
    public Dictionary<long, long> BuffEffectCds { get; } = new();
    public long ImmuneRecoveryUntilMs { get; set; }
    public int ImmuneRecoveryPercent { get; set; }
    public Dictionary<int, long> SkillCds { get; } = new();
    public Dictionary<int, long> ItemCds { get; } = new();
    public Dictionary<int, long> ItemGroupCds { get; } = new();

    /// <summary>Current HP / SP (null = full); maxima come from the stat sheet.</summary>
    public long? CurrentHp { get; set; }
    public long? CurrentSp { get; set; }

    /// <summary>GM override added to the speed attribute (113), not saved (!speed).</summary>
    public long SpeedBonus { get; set; }

    /// <summary>Between sending 70010 and the client's 30005: walks still refer to the old map and are dropped.</summary>
    public bool SceneLoading { get; set; }

    /// <summary>Time of the last accepted walk (speed check).</summary>
    public DateTime LastWalkUtc { get; set; }

    /// <summary>Walk check refusals of this session (logged sparsely).</summary>
    internal int WalkCheckWarnings { get; set; }

    /// <summary>The last accepted walk was a move, not a stop: the client is somewhere along it now.</summary>
    public bool Walking { get; set; }

    /// <summary>Level, exp, stats, currencies of the role in play (CharacterService).</summary>
    public RoleProgress? Progress { get; set; }

    /// <summary>Last successful skill-plan switch; the plans themselves use the shared DBSkillData below.</summary>
    public DateTime SkillPlanSwitchUtc { get; set; }

    /// <summary>Quest state (AUTO_SYNC 5027), loaded at login by TaskService.</summary>
    public Romsg.DBTaskInfo? Tasks { get; set; }
    public TaskRuntimeState TaskRuntime { get; set; } = new();
    public bool InCutScene { get => TaskRuntime.InCutScene; set => TaskRuntime.InCutScene = value; }

    /// <summary>Serializes quest transitions and source replay independently of the inventory transaction lock.</summary>
    public SemaphoreSlim TaskLock { get; } = new(1, 1);

    /// <summary>Achievements / Trophy (AUTO_SYNC 5019, role blobs "achievements" and "achievement_runtime"); AchievementService.</summary>
    public Romsg.DBRoleAchievementInfo? Achievements { get; set; }

    /// <summary>The role's own Adventure Group values (role blob "adventure_group", AUTO_SYNC 5068); AdventureGroupService.</summary>
    public Romsg.DBAdventureGroupRelationData? AdventureGroupRole { get; set; }
    public AchievementService.RuntimeState? AchievementRuntime { get; set; }

    /// <summary>Serializes achievement progress and claims (taken before, never inside, the character lock).</summary>
    public SemaphoreSlim AchievementLock { get; } = new(1, 1);

    /// <summary>To-Do rows and shared stage claims (5004/5059), loaded before quest login.</summary>
    public DailyActivityService.DailyState? Daily { get; set; }
    public Romsg.DBDailyActivityInfo? DailyRows { get; set; }
    public bool DailyReady { get; set; }
    /// <summary>Derived 5004 schedule refresh, independent of persisted progress/reset epochs.</summary>
    public long DailyWindowRefreshAt { get; set; }
    public long DailyWindowSampledAt { get; set; }

    /// <summary>Per-role seasonal activities (5013); the source lock serializes claims independently of inventory.</summary>
    public Romsg.DBNewActivityInfo? NewActivities { get; set; }
    public ActivityRuntimeState ActivityRuntime { get; set; } = new();
    public SemaphoreSlim ActivityGate { get; } = new(1, 1);
    public bool ActivitiesReady { get; set; }
    /// <summary>The client has the pass challenge rows (Ro3Emu.ClientHooks.PassChallenges sent 77014): 5027 may carry them.</summary>
    public bool PassChallengeRows { get; set; }
    public PronteraDefenseService.State? Defense { get; set; }
    public TimedBattleService.State? TimedBattle { get; set; }
    public int EventFaction { get; set; }
    public Romsg.DBTrainingGroundInfo? Training { get; set; }
    public long? ActivityServerDay { get; set; }
    public Romsg.DBLotteryData? Lottery { get; set; }
    public long LotteryDay { get; set; }

    /// <summary>Finished tutorial guides (AUTO_SYNC 5055), loaded at login by GuideService.</summary>
    public Romsg.DBGuideInfo? Guides { get; set; }

    /// <summary>Emote favourites, unlocks and idle emote (5006, role blob "emoji_play"); EmojiService.</summary>
    public Romsg.DBEmojiPlayInfo? EmojiPlay { get; set; }

    /// <summary>Chat emoticons unlocked by items (5041, role blob "emoji_info"); EmojiService.</summary>
    public Romsg.DBEmojiInfo? EmojiInfo { get; set; }

    /// <summary>Profile card presets and 620 item unlocks (5016, role blob "personal"; other unlockInfos are derived); PersonalService.</summary>
    public Romsg.DBPersonalInfo? Personal { get; set; }

    /// <summary>Chat cosmetics unlocked by items and the worn choices (role blob "social_outfit"; 5044 lays them over the
    /// configuration's defaults); SocialOutfitService.</summary>
    public Romsg.DBSocialOutfit? SocialOutfit { get; set; }

    /// <summary>The emote or sitting in progress (state 17 Prepare or 14 Play); runtime only, gone at logout.</summary>
    public EmojiService.Emote? Emote { get; set; }

    /// <summary>Last accepted emote request (PerformanceConfig._iPerformanceCD).</summary>
    public long LastEmoteRequestMs { get; set; }

    /// <summary>Owned pet roster and plans (AUTO_SYNC 5017).</summary>
    public Romsg.DBPetInfo? Pets { get; set; }

    /// <summary>Login prerequisites (5027/5017) must arrive before quest-gated 5056, including automatic quest rewards.</summary>
    public bool DeferFunctionSync { get; set; }
    /// <summary>
    /// Role selection holds 5027 until every owner has run its login changes (the day reset accepts the day's
    /// tracking quests). A 5027 increment before the first scene made the client's task handler refresh NPC
    /// sprites with no current map (LC_MapDataHelper.GetAllNPCData nil index on the first login after 05:00);
    /// one full 5027 at the end of the data phase carries those quests like any other login.
    /// </summary>
    public bool DeferTaskSync { get; set; }
    /// <summary>The card source has been loaded for this exact attachment, not a previous role/select.</summary>
    internal Ownership.AttachmentLease? CardLibraryReadyAttachment { get; set; }

    /// <summary>Transient deployed actor; visibility is owned by World, never persisted with the roster.</summary>
    public PetFollowerActor? PetActor { get; set; }

    /// <summary>Card collection (AUTO_SYNC 5057), loaded at login by CardService.</summary>
    public Romsg.DBLibraryInfo? Library { get; set; }
    /// <summary>Persisted card socket selections (AUTO_SYNC 5026).</summary>
    public Romsg.DBStorageHoleInfo? CardPlans { get; set; }

    public ShopService.ShopState? Shop { get; set; }

    /// <summary>Mount ownership/selection, wardrobe, Divine Totem and Build presets (5012/5034/5032/5008).</summary>
    public Romsg.DBMountsInfo? Mounts { get; set; }
    public MountService.SavedState MountState { get; set; } = new();

    /// <summary>Leased Special Class Items (5002, role blob "career_special"); CareerSpecialService.</summary>
    public Romsg.DBCareerSpecial? CareerSpecial { get; set; }

    /// <summary>Valhalla Trials Blessing statue (role blob "heroic_spirit"); ValhallaService. Cleared layers live in <see cref="Dungeons"/>.</summary>
    public Romsg.DBOdinStatue? OdinStatue { get; set; }

    /// <summary>The layer of the last admitted Valhalla challenge (5046 latestChallengeId; the client never reads it).</summary>
    public long ValhallaLatest { get; set; }

    /// <summary>Monster Hunt counters and claims (5053, role blob "area_concur"); MonsterHuntService.</summary>
    public Romsg.DBAreaConcurInfo? AreaConcur { get; set; }
    /// <summary>The bytes of <see cref="AreaConcur"/> as stored: a kill's staged write checks them in its transaction.</summary>
    public byte[] AreaConcurPersisted { get; set; } = [];
    /// <summary>A committed kill changed the counters; the 5053 sync follows the kill.</summary>
    public bool AreaConcurChanged { get; set; }

    /// <summary>Growth Classroom claimed rank rewards (role blob "growth_class", only rankReward); GrowthClassService.</summary>
    public Romsg.DBGrowthClassInfo? GrowthClass { get; set; }

    /// <summary>Seven-Day Goal tasks, score and claims (5049, role blob "first_heart"); FirstHeartService.</summary>
    public Romsg.DBFirstHeartTaskActivityData? FirstHeart { get; set; }

    /// <summary>The leased items' PendantConfig ids (EntityBase.pendantInfo), derived from <see cref="CareerSpecial"/>.</summary>
    public int[] CareerPendants { get; set; } = [];
    public Romsg.DBWardRobeInfo? Wardrobe { get; set; }
    public Romsg.DBTotemInfo? Totems { get; set; }
    public string TotemRefineEpoch { get; set; } = "";
    public Romsg.DBGenre? Genre { get; set; }
    public Romsg.DBSkillData? SkillPlans { get; set; }
    public DateTime TotemSwitchUtc { get; set; }
    /// <summary>5063 personal pre-buy counters and joined auction history.</summary>
    public Romsg.DBAuction? Auction { get; set; }

    /// <summary>5033 preferences and market counters; offers live in durable escrow, not this dataset.</summary>
    public Romsg.DBUserTradeInfo? Trade { get; set; }
    /// <summary>5058 world-stall metadata. The actual stock travels in 33408/33411.</summary>
    public Romsg.DBStallsInfo? Stalls { get; set; }
    public long ViewedStall { get; set; }
    public long ViewedSystemStall { get; set; }

    /// <summary>5047 role view, rebuilt from the exclusive guild membership index at login.</summary>
    public Romsg.DBRoleGuildInfo? Guild { get; set; }

    /// <summary>Spirit Tower progress (5065), including four party slots</summary>
    public Romsg.DBClimTower? Tower { get; set; }

    public TowerService.Attempt? TowerAttempt { get; set; }

    /// <summary>Ordinary dungeon progress/allowances (5062) and the current shared attempt.</summary>
    public DungeonService.State? Dungeons { get; set; }
    public long DungeonSyncMinute { get; set; }
    public DungeonService.Attempt? DungeonAttempt { get; set; }
    public GuildChallengeService.PersonalState? GuildChallenge { get; set; }

    /// <summary>GM test aid only: this role session may start a multi-dungeon match alone. Never persisted.</summary>
    public bool DungeonSoloMatch { get; set; }

    /// <summary>Territory war role counters and the shared, guild-qualified battle instance.</summary>
    public TerritoryWarsService.RoleState? TerritoryWars { get; set; }
    public TerritoryWarsService.Battle? TerritoryBattle { get; set; }
    public TerritoryWarsService.SiegeRun? SiegeBattle { get; set; }
    public TerritoryEscortService.RoleState? TerritoryEscort { get; set; }
    public bool TerritoryMinimapOpen { get; set; }

    /// <summary>Durable Soul Echo plans (AUTO_SYNC 5005) and attained power.</summary>
    public EmblemService.EmblemState Emblems { get; set; } = new();

    /// <summary>Current active Soul Echo plan's season score (power protocol).</summary>
    public long EmblemPower { get; set; }
    public DateTime EmblemPlanSwitchUtc { get; set; }

    /// <summary>Last published role power (PowerService, 31502/31503/5066); null until this role's login publish.</summary>
    public PowerService.Snapshot? Power { get; set; }
    internal long PowerPublishedMs { get; set; }

    /// <summary>Fills the stored plan power of 5007/5025/5026/5067 before the dataset is diffed (PowerService).</summary>
    internal Action<uint, IMessage>? PlanPowers { get; set; }

    /// <summary>Durable talent plans and oath (AUTO_SYNC 5050), loaded for the selected role.</summary>
    public Romsg.DBSeasonTalent? SeasonTalent { get; set; }

    /// <summary>Season page claims/honor (5022) and grid refinement (5023), selected-role state.</summary>
    public Romsg.DBSeasonPage? SeasonPage { get; set; }
    public Romsg.DBSeasonRefine? SeasonRefine { get; set; }

    /// <summary>Season/playbook captured at role load, then advanced with its 5021/82708 clock notifications.</summary>
    public Romsg.SeasonState? SeasonState { get; set; }

    /// <summary>MG_Lifejob: persisted profession state and the restriction-backed vigor balance.</summary>
    public Romsg.DBLifeJobInfo? LifeJob { get; set; }
    public Romsg.DBRestrictInfo? Restrictions { get; set; }
    public LifeJobService.RuntimeState LifeRuntime { get; set; } = new();

    /// <summary>Current playbook and delivered tasks permit this role's purchased talent attributes.</summary>
    public bool TalentEffectsEnabled { get; set; }

    /// <summary>Last accepted 83552 switch in this session (PublicParamConfig.SchemeCD).</summary>
    public DateTime TalentPlanSwitchUtc { get; set; }

    public EquipLevels EquipLevels { get; set; } = new();

    /// <summary>5007 plan references/names/current index; instances remain exclusively in Items.</summary>
    public Romsg.DBEquipInfo EquipmentPlans { get; set; } = new();

    /// <summary>5009 autoDecomposeSetting, loaded before the first item sync.</summary>
    public Romsg.AutoDecomposeSetting AutoDecompose { get; set; } = new();

    /// <summary>Durable quick-use, medicine, ammunition and shortcut selections (5009).</summary>
    public RoleBagState BagState { get; set; } = new(null);

    /// <summary>Durable attribute allocations and names for every 5067 stat plan.</summary>
    public StatPlanState StatPlans { get; set; } = new(null);

    /// <summary>31054 SKILL_SET_AUTO_BATTLE: the client's auto-battle switch.</summary>
    public bool AutoBattle { get; set; }

    /// <summary>5035 field Bonus Time, daily consumption and drop history; transient activity never survives logout.</summary>
    public WildAfkService.State? WildAfk { get; set; }

    /// <summary>Property list (AoIProperty idx, value) for ENTITYSELF / AOI bases, kept current by CharacterService.Refresh.</summary>
    public (long Id, long Value)[] EntityProperties { get; set; } = [];

    /// <summary>Property ids already sent in 31008; retained across local Refresh until a zero is sent.</summary>
    public HashSet<int> SentPropertyIds { get; } = new();

    /// <summary>EntityBase.equipInfo: item ids of equipment grids 1..7.</summary>
    public long[] EquipLook { get; set; } = [];

    /// <summary>Items of the role in play, bag and equipped (CharacterService).</summary>
    public List<InvItem> Items { get; set; } = new();

    /// <summary>The last version of each AUTO_SYNC dataset the client got (msg id -> DB object), for AutoSync increments.</summary>
    public Dictionary<uint, Google.Protobuf.IMessage> Synced { get; } = new();

    /// <summary>Items still marked new in the bag (guidToNew, the red dot).</summary>
    public HashSet<long> NewItems { get; } = new();

    /// <summary>Last AUTO_SYNC version sent per message id.</summary>
    public Dictionary<uint, long> SyncVersions { get; } = new();

    /// <summary>Serializes changes to the role state (game messages vs. GM commands from the HTTP endpoint).</summary>
    public SemaphoreSlim StateLock { get; } = new(1, 1);

    /// <summary>Distinguishes successive ClientAccount entities of one connection (role switching).</summary>
    public int AccountEntitySerial { get; set; }

    /// <summary>Other players this client currently shows (guarded by World's lock). Visibility is directed: a
    /// client with a lower view cap can show fewer players than see it (<see cref="Observers"/>).</summary>
    public HashSet<GameSession> Visible { get; } = new();

    /// <summary>Players whose clients show this one: the reverse of their <see cref="Visible"/> sets. Broadcasts
    /// about this player (movement, attributes, effects) go to these.</summary>
    public HashSet<GameSession> Observers { get; } = new();

    /// <summary>Most other players this client shows: its 30005 / 30007 viewMaxCount (min of the quality tier cap
    /// 39/49/79/99/149 and MaxOnScreenPlayers, LC_TransitionBase), clamped to 1..149.</summary>
    public int ViewLimit { get; set; } = Maps.World.MaxViewLimit;

    /// <summary>Server units (monsters...) this client currently shows.</summary>
    public HashSet<Maps.WorldUnit> VisibleUnits { get; } = new();

    public Task SendRpcAsync(string method, params object?[] args)
    {
        if (!CanPresent()) return Task.CompletedTask;
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        foreach (var arg in args) MessagePackSerializer.Serialize(arg?.GetType() ?? typeof(object), ref writer, arg);
        writer.Flush();
        traffic?.Rpc(Account, "s2c-rpc", method, () => string.Join(", ", args.Select(a => a?.ToString() ?? "nil")));
        return Channel.SendAsync(GameGateServer.EntityMessageOut, new EntityMessage
        {
            MethodName = ByteString.CopyFromUtf8(method),
            Parameters = ByteString.CopyFrom(buffer.WrittenSpan),
        });
    }

    /// <summary>Chat groups this player joined (guarded by ChatService's lock).</summary>
    public HashSet<string> ChatGroups { get; } = new();

    private bool CanPresent() => Owners?.AllowsPresentation(this) != false
        && (Owners?.Current?.CanPresent(this) ?? true);

    public Task SendGameAsync(uint msgId, IMessage body)
    {
        if (!CanPresent()) return Task.CompletedTask;
        var bytes = body.ToByteArray();
        traffic?.ServerToClient(Account, msgId, body.Descriptor, bytes);
        return SendGameBytesAsync(msgId, bytes);
    }

    /// <summary>A message serialized once for many recipients (AOI broadcasts).</summary>
    public Task SendPreparedAsync(PreparedMessage prepared)
    {
        if (!CanPresent()) return Task.CompletedTask;
        if (Owners?.Current?.Notices is { } notices)
        {
            DeferPrepared(notices, prepared);
            return Task.CompletedTask;
        }
        traffic?.ServerToClient(Account, prepared.MsgId, prepared.Type, prepared.Body);
        Diagnostics.MessageCoverage.Outbound(prepared.MsgId);
        using var measurement = Channel.Metrics?.Outgoing(prepared.MsgId, prepared.Body.Length);
        return Channel.SendFrameAsync(prepared.Frame);
    }

    // The closure lives here so that the common path (no notices) does not allocate it on every send (stage G).
    private void DeferPrepared(Ownership.OwnerNotices notices, PreparedMessage prepared) =>
        notices.Add(this, () => SendPreparedAsync(prepared));

    /// <summary>A game message whose body is not a romsg protobuf (the chat tunnel).</summary>
    public Task SendGameRawAsync(uint msgId, byte[] body)
    {
        if (!CanPresent()) return Task.CompletedTask;
        traffic?.ServerToClientRaw(Account, msgId, body.Length);
        return SendGameBytesAsync(msgId, body);
    }

    private Task SendGameBytesAsync(uint msgId, byte[] bytes)
    {
        if (!CanPresent()) return Task.CompletedTask;
        if (Owners?.Current?.Notices is { } notices)
        {
            notices.Add(this, () => SendGameBytesAsync(msgId, bytes));
            return Task.CompletedTask;
        }
        Diagnostics.MessageCoverage.Outbound(msgId);
        using var measurement = Channel.Metrics?.Outgoing(msgId, bytes.Length);
        var parameters = new byte[4 + bytes.Length];
        BitConverter.TryWriteBytes(parameters, msgId);
        bytes.CopyTo(parameters, 4);
        return Channel.SendAsync(GameGateServer.EntityMessageOut, new EntityMessage { Parameters = ByteString.CopyFrom(parameters) });
    }

    /// <summary>Creates the client-side entity ("ClientAccount" before role select, "ClientAvatar" after).
    /// <paramref name="reconnectInfo"/> (field 8) becomes the client's GameClient.ReconnectInfo.</summary>
    public Task CreateChannelEntityAsync(string type, string entityId, long channelId, byte[]? reconnectInfo = null)
    {
        var info = new ChannelEntityInfo
        {
            Type = ByteString.CopyFromUtf8(type),
            EntityId = ByteString.CopyFromUtf8(entityId),
            ChannelId = channelId,
            ChannelMode = 0,
        };
        if (reconnectInfo != null) info.ReconnectInfo = ByteString.CopyFrom(reconnectInfo);
        return Channel.SendAsync(GameGateServer.CreateChannelEntity, info);
    }
}
