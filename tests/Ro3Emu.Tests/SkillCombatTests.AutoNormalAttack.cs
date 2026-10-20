using Microsoft.Extensions.DependencyInjection;
using Romsg;
using Ro3Emu.Server;
using Ro3Emu.Server.Game;

namespace Ro3Emu.Tests;

public sealed partial class SkillCombatTests
{
    /// <summary>
    /// SkillConfig._iIfAutoAtk 2 (enum_AutoNormalAttackType.AutoNormalAttack) on an active skill: the client follows
    /// the cast with the career's normal attack (LC_AutoAttackAppendState: GetNormalSkill). The server's attack loop
    /// repeated Blade Smash itself every swing, past its 7 s cooldown.
    /// </summary>
    [ClientDataFact]
    public async Task AutoNormalAttackSkillIsFollowedByTheNormalAttackNotRepeatedPastItsCooldown()
    {
        const int bladeSmash = 1509901; // group 15099, _iIfAutoAtk 2, _iIsNormalAtk 0, _iCD 7000
        await using var alice = await InWorld("Alice");
        await Gm("alice", "!job 5100"); await Teach("alice", 15099);
        int normal = app.Services.GetRequiredService<SkillService>().NormalAttack(5100);
        Assert.NotEqual(bladeSmash, normal);
        var target = await Target("alice", stationary: true);
        await Loop.InvokeAsync(() => { target.Hp = 1_000_000_000; return Task.CompletedTask; });

        await alice.SendGameAsync(CombatService.UseSkill, new CastSkillReq { SkillId = bladeSmash, TargetId = target.Uid });
        var accepted = await Next(alice, CombatService.UseSkill, CastSkillRsp.Parser);
        Assert.Equal(0, accepted.Result);
        Assert.Equal(bladeSmash, (await Next(alice, CombatService.UseInSkill, UseInSkillInfo.Parser,
            u => u.SessionId == accepted.SessionId)).SkillID);

        // The loop's next swings, well inside the cooldown, are normal attacks on the same target.
        long self = await Loop.InvokeAsync(() => Task.FromResult(Session("alice").Role!.Id));
        for (int i = 0; i < 2; i++)
        {
            var swing = await Next(alice, CombatService.UseInSkill, UseInSkillInfo.Parser,
                u => u.SrcID == self && u.SessionId != accepted.SessionId);
            Assert.Equal(normal, swing.SkillID);
            Assert.Equal(target.Uid, swing.TargetId);
        }
        await Loop.InvokeAsync(() => { Assert.Equal(normal, Session("alice").Attack?.SkillId); return Task.CompletedTask; });

        await alice.SendGameAsync(CombatService.UseSkill, new CastSkillReq { SkillId = bladeSmash, TargetId = target.Uid });
        Assert.Equal(ErrorCodes.SkillCooldown, (await Next(alice, CombatService.UseSkill, CastSkillRsp.Parser)).Result);
    }

    [ClientDataFact]
    public async Task RepeatedNormalAttackSpamIsRefusedWithSkillBusy()
    {
        await using var alice = await InWorld("Alice");
        await Gm("alice", "!job 1100");
        int normal = app.Services.GetRequiredService<SkillService>().NormalAttack(1100);
        var target = await Target("alice", stationary: true);
        await Loop.InvokeAsync(() => { target.Hp = 1_000_000_000; return Task.CompletedTask; });

        // First manual normal attack is accepted.
        await alice.SendGameAsync(CombatService.UseSkill, new CastSkillReq { SkillId = normal, TargetId = target.Uid });
        var first = await Next(alice, CombatService.UseSkill, CastSkillRsp.Parser);
        Assert.Equal(0, first.Result);

        // Immediate subsequent click (spamming while swing is still in flight / ASPD cooldown) is refused as busy.
        await alice.SendGameAsync(CombatService.UseSkill, new CastSkillReq { SkillId = normal, TargetId = target.Uid });
        var spam = await Next(alice, CombatService.UseSkill, CastSkillRsp.Parser);
        Assert.Equal(ErrorCodes.SkillBusy, spam.Result);
    }
}
