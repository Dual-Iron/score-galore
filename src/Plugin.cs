using BepInEx;
using Menu;
using MoreSlugcats;
using RWCustom;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Permissions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ScoreGalore;

[BepInPlugin("com.dual.score-galore", "Score Galore", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // Food             +1
    // Survived cycle   +10
    // Died in cycle    -3
    // Quit cycle       -3
    // Minute passed    -1
    // Hunter payload   +100
    // Hunter 5P        +40
    // Ascending        +300

    // Meeting LttM     +40
    // Meeting 5P       +40
    // Pearl read       +20
    // Gourmand quest   +300
    // Sleep w/friend   +15

    public static int CurrentCycleScore;
    public static int CurrentAverageScore;

    int currentCycleTime;

    SlugcatStats.Name viewStats;

    public static string FmtAdd(int add) => add > 0 ? "+" + add : add.ToString();

    private static int[] killScores;
    private static int[] KillScores()
    {
        if (killScores == null || killScores.Length != ExtEnum<MultiplayerUnlocks.SandboxUnlockID>.values.Count) {
            killScores = new int[ExtEnum<MultiplayerUnlocks.SandboxUnlockID>.values.Count];
            for (int i = 0; i < killScores.Length; i++) {
                killScores[i] = 1;
            }
            SandboxSettingsInterface.DefaultKillScores(ref killScores);
            killScores[(int)MultiplayerUnlocks.SandboxUnlockID.Slugcat] = 1;
        }
        return killScores;
    }

    public static int KillScore(IconSymbol.IconSymbolData iconData)
    {
        var s = (StoryGameStatisticsScreen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StoryGameStatisticsScreen));
        var score = s.GetNonSandboxKillscore(iconData.critType);

        if (score == 0 && MultiplayerUnlocks.SandboxUnlockForSymbolData(iconData) is MultiplayerUnlocks.SandboxUnlockID unlockID) {
            score = KillScores()[unlockID.Index];
        }

        return score;
    }

    public static Color ScoreTextColor(int score, int targetScore) => new HSLColor(Custom.LerpMap(score, 0f, targetScore, 0f, 200f / 360f), 0.7f, 0.7f).rgb;

    private ScoreCounter GetCounter(RainWorldGame game)
    {
        return game?.session is StoryGameSession ? game.cameras[0]?.hud?.parts.OfType<ScoreCounter>().FirstOrDefault() : null;
    }

    private int GetTotalScore(SaveState saveState)
    {
        if (saveState == null) {
            return 0;
        }
        return saveState.totFood + saveState.cycleNumber * 10 + saveState.kills.Sum(kvp => KillScore(kvp.Key) * kvp.Value)
            - saveState.deathPersistentSaveData.deaths * 3 
            - saveState.deathPersistentSaveData.quits * 3
            - saveState.totTime / 60;
    }

    private int GetAverageScore(SaveState saveState)
    {
        if (saveState == null) {
            return 0;
        }
        int numerator = GetTotalScore(saveState);
        int denominator = saveState.cycleNumber + saveState.deathPersistentSaveData.deaths + saveState.deathPersistentSaveData.quits;
        return Mathf.RoundToInt((float)numerator / denominator);
    }

    public void OnEnable()
    {
        // Real-time score tracking
        On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
        On.SocialEventRecognizer.Killing += SocialEventRecognizer_Killing;
        On.Player.AddFood += Player_AddFood;
        On.Player.SubtractFood += Player_SubtractFood;
        On.StoryGameSession.TimeTick += StoryGameSession_TimeTick;

        // Sleep screen score trackers
        On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;

        // View statistics screen
        On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
        On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;
        On.Menu.StoryGameStatisticsScreen.AddBkgIllustration += StoryGameStatisticsScreen_AddBkgIllustration;
    }

    private void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
    {
        orig(self, cam);

        CurrentCycleScore = 10;
        CurrentAverageScore = GetAverageScore(self.rainWorld.progression.currentSaveState);

        self.AddPart(new ScoreCounter(self) {
            Score = CurrentCycleScore,
        });
    }

    private void SocialEventRecognizer_Killing(On.SocialEventRecognizer.orig_Killing orig, SocialEventRecognizer self, Creature killer, Creature victim)
    {
        orig(self, killer, victim);

        if (killer is Player && CreatureSymbol.DoesCreatureEarnATrophy(victim.Template.type)) {
            GetCounter(self.room.game)?.AddKill(victim);
        }
    }

    private void Player_AddFood(On.Player.orig_AddFood orig, Player self, int add)
    {
        if (self.abstractCreature.world.game.session is StoryGameSession story) {
            int before = story.saveState.totFood;
            orig(self, add);
            int after = story.saveState.totFood;

            GetCounter(story.game)?.AddBonus(new() { Add = after - before, Color = Color.white });
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

        int minute = self.playerSessionRecords[0].time / 2400;

        if (currentCycleTime < minute && GetCounter(self.game) is ScoreCounter counter) {
            counter.AddBonus(new() { Add = currentCycleTime - minute, Color = new(0.7f, 0.7f, 0.7f) });
            currentCycleTime = minute;
        }
    }

    private void SleepAndDeathScreen_GetDataFromGame(On.Menu.SleepAndDeathScreen.orig_GetDataFromGame orig, SleepAndDeathScreen self, KarmaLadderScreen.SleepDeathScreenDataPackage package)
    {
        orig(self, package);

        Vector2 topLeft = new(self.LeftHandButtonsPosXAdd, self.continueButton.pos.y + 120);

        int current = CurrentCycleScore;
        int total = GetTotalScore(package.saveState);
        int oldAverage = CurrentAverageScore;
        int newAverage = GetAverageScore(package.saveState);

        if (self.IsAnyDeath) {
            // Start with 10 (for surviving), but deaths are a -3. Does not count time.
            current = -3;
        }

        self.pages[0].subObjects.Add(new ScoreTicker(self.pages[0], topLeft, self.Translate("Cycle :")) {
            start = 0,
            end = current,
            fmtAdd = true,
            numberColor = ScoreTextColor(current, oldAverage)
        });

        self.pages[0].subObjects.Add(new ScoreTicker(self.pages[0], topLeft - new Vector2(0, 30), self.Translate("Total :")) {
            start = total - current,
            end = total,
            animationClock = -40,
        });

        self.pages[0].subObjects.Add(new ScoreTicker(self.pages[0], topLeft - new Vector2(0, 60), self.Translate("Average :")) {
            start = oldAverage,
            end = newAverage,
            animationClock = -80,
        });
    }

    private void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, SlugcatSelectMenu self, ProcessManager manager)
    {
        orig(self, manager);

        float posX = self.startButton.pos.x - 200f - SlugcatSelectMenu.GetRestartTextOffset(self.CurrLang);

        self.pages[0].subObjects.Add(new StatCheckBox(self, posX));
    }

    private void SlugcatSelectMenu_StartGame(On.Menu.SlugcatSelectMenu.orig_StartGame orig, SlugcatSelectMenu self, SlugcatStats.Name storyGameCharacter)
    {
        StatCheckBox stats = self.pages[0].subObjects.OfType<StatCheckBox>().FirstOrDefault();
        if (stats != null && stats.isChecked && self.manager.rainWorld.progression.IsThereASavedGame(storyGameCharacter)) {
            SetJollyColors(self);

            self.manager.arenaSitting = null;
            self.manager.rainWorld.progression.currentSaveState = null;
            self.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat = storyGameCharacter;

            self.redSaveState = self.manager.rainWorld.progression.GetOrInitiateSaveState(storyGameCharacter, null, self.manager.menuSetup, false);
            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Statistics);
            self.PlaySound(SoundID.MENU_Switch_Page_Out);

            viewStats = storyGameCharacter;

            if (self.manager.musicPlayer?.song is Music.IntroRollMusic) {
                self.manager.musicPlayer.song.FadeOut(20f);
            }
        }
        else {
            orig(self, storyGameCharacter);
        }

        static void SetJollyColors(SlugcatSelectMenu self)
        {
            var m = self.manager.rainWorld.progression.miscProgressionData;
            var text = self.slugcatColorOrder[self.slugcatPageIndex].value;

            if (ModManager.MMF && m.colorsEnabled.TryGetValue(text, out bool v) && v) {
                List<Color> colors = new();
                for (int i = 0; i < m.colorChoices[text].Count; i++) {
                    Vector3 vector = new(1f, 1f, 1f);
                    if (m.colorChoices[text][i].Contains(",")) {
                        string[] array = m.colorChoices[text][i].Split(',');
                        vector = new Vector3(
                            float.Parse(array[0], NumberStyles.Any, CultureInfo.InvariantCulture),
                            float.Parse(array[1], NumberStyles.Any, CultureInfo.InvariantCulture),
                            float.Parse(array[2], NumberStyles.Any, CultureInfo.InvariantCulture)
                            );
                    }
                    colors.Add(Custom.HSL2RGB(vector[0], vector[1], vector[2]));
                }
                PlayerGraphics.customColors = colors;
            }
            else {
                PlayerGraphics.customColors = null;
            }
        }
    }

    private void StoryGameStatisticsScreen_AddBkgIllustration(On.Menu.StoryGameStatisticsScreen.orig_AddBkgIllustration orig, StoryGameStatisticsScreen self)
    {
        if (viewStats == null) {
            orig(self);
            return;
        }

        SlugcatSelectMenu.SaveGameData saveGameData = SlugcatSelectMenu.MineForSaveData(self.manager, viewStats);

        if (saveGameData != null && saveGameData.ascended && (!ModManager.MSC || RainWorld.lastActiveSaveSlot != MoreSlugcatsEnums.SlugcatStatsName.Saint)) {
            self.scene = new InteractiveMenuScene(self, self.pages[0], MenuScene.SceneID.Red_Ascend);
            self.pages[0].subObjects.Add(self.scene);
        }
        else if (RainWorld.lastActiveSaveSlot == SlugcatStats.Name.Red && saveGameData.redsDeath) {
            self.scene = new InteractiveMenuScene(self, self.pages[0], MenuScene.SceneID.RedsDeathStatisticsBkg);
            self.pages[0].subObjects.Add(self.scene);
        }
        else {
            self.scene = new InteractiveMenuScene(self, self.pages[0], MenuScene.SceneID.SleepScreen);
            self.pages[0].subObjects.Add(self.scene);
        }

        viewStats = null;
    }
}
