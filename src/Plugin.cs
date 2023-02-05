using BepInEx;
using JetBrains.Annotations;
using System.Linq;
using System.Security.Permissions;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ScoreGalore;

[BepInPlugin("com.dual.score-galore", "Score Galore", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    private ScoreCounter GetCounter(RainWorldGame game)
    {
        return game?.session is StoryGameSession ? game.cameras[0]?.hud?.parts.OfType<ScoreCounter>().FirstOrDefault() : null;
    }

    public void OnEnable()
    {
        On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
        On.SocialEventRecognizer.Killing += SocialEventRecognizer_Killing;
        On.Player.AddFood += Player_AddFood;
        On.Player.SubtractFood += Player_SubtractFood;
        On.StoryGameSession.TimeTick += StoryGameSession_TimeTick;
    }

    private void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
    {
        orig(self, cam);

        self.AddPart(new ScoreCounter(self));
    }

    private void SocialEventRecognizer_Killing(On.SocialEventRecognizer.orig_Killing orig, SocialEventRecognizer self, Creature killer, Creature victim)
    {
        orig(self, killer, victim);

        if (killer is Player) {
            GetCounter(self.room.game)?.AddKill(victim);
        }
    }

    private void Player_AddFood(On.Player.orig_AddFood orig, Player self, int add)
    {
        if (self.abstractCreature.world.game.session is StoryGameSession story) {
            int before = story.saveState.totFood;
            orig(self, add);
            int after = story.saveState.totFood;

            GetCounter(story.game)?.AddBonus(new() { Add = after - before, Color = UnityEngine.Color.white });
        }
        else {
            orig(self, add);
        }
    }

    private void Player_SubtractFood(On.Player.orig_SubtractFood orig, Player self, int sub)
    {
        if (self.abstractCreature.world.game.session is StoryGameSession story) {
            int before = story.saveState.totFood;
            orig(self, sub);
            int after = story.saveState.totFood;

            GetCounter(story.game)?.AddBonus(new() { Add = after - before, Color = new UnityEngine.Color(0.40f, 0.55f, 0.12f) });
        }
        else {
            orig(self, sub);
        }
    }

    private void StoryGameSession_TimeTick(On.StoryGameSession.orig_TimeTick orig, StoryGameSession self, float dt)
    {
        orig(self, dt);

        if (GetCounter(self.game) is ScoreCounter counter) {
            int minute = self.playerSessionRecords[0].time / 2400;

            if (counter.lastMinute < minute) {
                counter.AddBonus(new() { Add = counter.lastMinute - minute, Color = new(0.7f, 0.7f, 0.7f) });
                counter.lastMinute = minute;
            }
        }
    }
}
