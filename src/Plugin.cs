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

[BepInPlugin("com.dual.score-galore", "Score Galore", "1.0.3")]
sealed class Plugin : BaseUnityPlugin
{
    // -- Vanilla --
    // Food             +1
    // Survived cycle   +10
    // Died in cycle    -3
    // Quit cycle       -3
    // Minute passed    -1
    // Hunter payload   +100
    // Hunter 5P        +40
    // Ascending        +300
    // -- MSC exclusive --
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
        if (!CreatureSymbol.DoesCreatureEarnATrophy(iconData.critType)) {
            return 0;
        }

        var s = (StoryGameStatisticsScreen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StoryGameStatisticsScreen));
        var score = s.GetNonSandboxKillscore(iconData.critType);

        if (score == 0 && MultiplayerUnlocks.SandboxUnlockForSymbolData(iconData) is MultiplayerUnlocks.SandboxUnlockID unlockID) {
            score = KillScores()[unlockID.Index];
        }

        return score;
    }

    public static Color ScoreTextColor(int score, int targetScore)
    {
        return new HSLColor(Custom.LerpMap(score, 0f, targetScore * 2f, 0f, 240f / 360f), 0.7f, 0.7f).rgb;
    }

    private void AddCurrentCycleScore(RainWorldGame game, int score, IconSymbol.IconSymbolData? icon)
    {
        if (score == 0) return;
        if (icon == null) {
            AddCurrentCycleScore(game, score, Color.white);
            return;
        }

        Color color = icon.Value.itemType == AbstractPhysicalObject.AbstractObjectType.Creature
            ? CreatureSymbol.ColorOfCreature(icon.Value)
            : ItemSymbol.ColorForItem(icon.Value.itemType, icon.Value.intData);

        if (game?.cameras[0]?.hud?.parts.OfType<ScoreCounter>().FirstOrDefault() is ScoreCounter counter) {
            counter.AddBonus(score, color, icon, false);
        }
        else {
            CurrentCycleScore += score;
        }
    }
    private void AddCurrentCycleScore(RainWorldGame game, int score, Color color, bool stacks = false)
    {
        if (score == 0) return;

        if (game?.cameras[0]?.hud?.parts.OfType<ScoreCounter>().FirstOrDefault() is ScoreCounter counter) {
            counter.AddBonus(score, color, null, stacks);
        }
        else {
            CurrentCycleScore += score;
        }
    }

    private int MSC(int score) => ModManager.MSC ? score : 0;

    private int GetTotalScore(SaveState s)
    {
        if (s == null) {
            return 0;
        }

        var d = s.deathPersistentSaveData;
        var red = s.saveStateNumber == SlugcatStats.Name.Red;
        var arti = s.saveStateNumber == MoreSlugcatsEnums.SlugcatStatsName.Artificer;

        int vanilla = s.totFood + d.survives * 10 + s.kills.Sum(kvp => KillScore(kvp.Key) * kvp.Value)
            - (d.deaths * 3 + d.quits * 3 + s.totTime / 60)
            + (d.ascended ? 300 : 0)
            + (s.miscWorldSaveData.moonRevived ? 100 : 0)
            + (s.miscWorldSaveData.pebblesSeenGreenNeuron ? 40 : 0);

        int msc = (!arti ? d.friendsSaved * 15 : 0)
            + (!red ? s.miscWorldSaveData.SLOracleState.significantPearls.Count * 20 : 0)
            + (!red && !arti && s.miscWorldSaveData.SSaiConversationsHad > 0 ? 40 : 0)
            + (!red && !arti && s.miscWorldSaveData.SLOracleState.playerEncounters > 0 ? 40 : 0)
            + (d.winState.GetTracker(MoreSlugcatsEnums.EndgameID.Gourmand, false) is WinState.GourFeastTracker { GoalFullfilled: true } ? 300 : 0);

        return vanilla + MSC(msc);
    }

    private int GetAverageScore(SaveState saveState)
    {
        if (saveState == null) {
            return 0;
        }
        int denominator = saveState.deathPersistentSaveData.survives + saveState.deathPersistentSaveData.deaths + saveState.deathPersistentSaveData.quits;
        if (denominator == 0) {
            return 10;
        }
        int numerator = GetTotalScore(saveState);
        return Mathf.RoundToInt((float)numerator / denominator);
    }

    public void OnEnable()
    {
        // -- Real-time score tracking --
        On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;

        // Track killing, eating, vomiting, passage of time, friends
        On.SocialEventRecognizer.Killing += CountKills;
        On.PlayerSessionRecord.AddEat += CountGourd;
        On.Player.AddFood += CountEat;
        On.Player.SubtractFood += CountVomit;
        On.StoryGameSession.TimeTick += CountTime;
        On.SaveState.SessionEnded += CountFriendsSaved;
        On.Oracle.Update += CountMoonand5P;
        On.SLOracleWakeUpProcedure.Update += CountReviveMoon;
        On.SLOracleBehaviorHasMark.GrabObject += CountPearl;
        On.SSOracleBehavior.StartItemConversation += CountPearl5P;

        // -- Sleep screen score trackers --
        On.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;

        // -- View statistics screen --
        On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
        On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;
        On.Menu.SlugcatSelectMenu.ComingFromRedsStatistics += SlugcatSelectMenu_ComingFromRedsStatistics;
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

    private void CountKills(On.SocialEventRecognizer.orig_Killing orig, SocialEventRecognizer self, Creature killer, Creature victim)
    {
        orig(self, killer, victim);

        if (killer is Player && self.room.game.IsStorySession) {
            IconSymbol.IconSymbolData icon = CreatureSymbol.SymbolDataFromCreature(victim.abstractCreature);

            AddCurrentCycleScore(self.room.game, KillScore(icon), icon);
        }
    }

    private void CountGourd(On.PlayerSessionRecord.orig_AddEat orig, PlayerSessionRecord self, PhysicalObject eatenObject)
    {
        if (eatenObject.room.game.session is StoryGameSession s && s.saveState.deathPersistentSaveData.winState.GetTracker(MoreSlugcatsEnums.EndgameID.Gourmand, false) is WinState.GourFeastTracker g) {
            for (int i = 0; i < g.currentCycleProgress.Length - 1; i++) {
                g.currentCycleProgress[i] = 1;
            }

            bool gourdBefore = g.currentCycleProgress.All(n => n > 0);

            orig(self, eatenObject);

            // "Food quest completed"
            if (!gourdBefore && g.currentCycleProgress.All(n => n > 0)) {
                AddCurrentCycleScore(s.game, MSC(300), CreatureSymbol.SymbolDataFromCreature(s.Players[self.playerNumber]));
            }
        }
        else {
            orig(self, eatenObject);
        }
    }

    private void CountEat(On.Player.orig_AddFood orig, Player self, int add)
    {
        if (self.abstractCreature.world.game.session is not StoryGameSession story) {
            orig(self, add);
            return;
        }

        int before = story.saveState.totFood;

        orig(self, add);

        int after = story.saveState.totFood;

        AddCurrentCycleScore(story.game, after - before, Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey), stacks: true);
    }

    private void CountVomit(On.Player.orig_SubtractFood orig, Player self, int sub)
    {
        if (self.abstractCreature.world.game.session is StoryGameSession story) {
            int before = story.saveState.totFood;
            orig(self, sub);
            int after = story.saveState.totFood;

            AddCurrentCycleScore(story.game, after - before, new Color(0.61f, 0.83f, 0.16f), stacks: true);
        }
        else {
            orig(self, sub);
        }
    }

    private void CountTime(On.StoryGameSession.orig_TimeTick orig, StoryGameSession self, float dt)
    {
        orig(self, dt);

        int minute = self.playerSessionRecords[0].time / 2400;

        if (currentCycleTime < minute) {
            AddCurrentCycleScore(self.game, currentCycleTime - minute, new(0.66f, 0.6f, 0.6f), stacks: true);
            currentCycleTime = minute;
        }
    }

    private void CountFriendsSaved(On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game, bool survived, bool newMalnourished)
    {
        int friendsSavedBefore = self.deathPersistentSaveData.friendsSaved;

        orig(self, game, survived, newMalnourished);

        // "Friends sheltered"
        if (self.saveStateNumber != MoreSlugcatsEnums.SlugcatStatsName.Artificer) {
            CurrentCycleScore += 15 * MSC(self.deathPersistentSaveData.friendsSaved - friendsSavedBefore);
        }
    }

    private void CountMoonand5P(On.Oracle.orig_Update orig, Oracle self, bool eu)
    {
        MiscWorldSaveData m = self.room.game.GetStorySession.saveState.miscWorldSaveData;

        bool sawNeuron = m.pebblesSeenGreenNeuron;
        int before5P = m.SSaiConversationsHad;
        int beforeLttM = m.SLOracleState.playerEncounters;

        orig(self, eu);

        // "Met Looks to the Moon" and "Met Five Pebbles"
        if (self.room.game.StoryCharacter != SlugcatStats.Name.Red && self.room.game.StoryCharacter != MoreSlugcatsEnums.SlugcatStatsName.Artificer) {
            if (before5P == 0 && m.SSaiConversationsHad > 0) {
                AddCurrentCycleScore(self.room.game, MSC(40), new Color(1f, 0.4f, 0.8f));
            }
            if (beforeLttM == 0 && m.SLOracleState.playerEncounters > 0) {
                AddCurrentCycleScore(self.room.game, MSC(40), new Color(0.12f, 0.45f, 0.55f));
            }
        }
        // "Helped Five Pebbles"
        if (!sawNeuron && m.pebblesSeenGreenNeuron) {
            AddCurrentCycleScore(self.room.game, 40, new Color(1f, 0.4f, 0.8f));
        }
    }

    private void CountReviveMoon(On.SLOracleWakeUpProcedure.orig_Update orig, SLOracleWakeUpProcedure self, bool eu)
    {
        MiscWorldSaveData m = self.room.game.GetStorySession.saveState.miscWorldSaveData;

        var neuron = self.resqueSwarmer;
        bool revived = m.moonRevived;

        orig(self, eu);

        // "Delivered Payload"
        if (!revived && m.moonRevived) {
            AddCurrentCycleScore(self.room.game, 100, ItemSymbol.SymbolDataFromItem(neuron.abstractPhysicalObject));
        }
    }

    private void CountPearl(On.SLOracleBehaviorHasMark.orig_GrabObject orig, SLOracleBehaviorHasMark self, PhysicalObject item)
    {
        bool read = item is DataPearl p && self.State.significantPearls.Contains(p.AbstractPearl.dataPearlType);

        orig(self, item);

        // "Unique pearls read"
        if (!read && item is DataPearl pearl && self.State.significantPearls.Contains(pearl.AbstractPearl.dataPearlType) && self.oracle.room.game.StoryCharacter != SlugcatStats.Name.Red) {
            AddCurrentCycleScore(self.oracle.room.game, MSC(20), ItemSymbol.SymbolDataFromItem(pearl.abstractPhysicalObject));
        }
    }

    private void CountPearl5P(On.SSOracleBehavior.orig_StartItemConversation orig, SSOracleBehavior self, DataPearl item)
    {
        var state = self.oracle.room.game.GetStorySession.saveState.miscWorldSaveData.SLOracleState;

        bool read = item is DataPearl p && state.significantPearls.Contains(p.AbstractPearl.dataPearlType);

        orig(self, item);

        // "Unique pearls read" for arti
        if (!read && item is DataPearl pearl && state.significantPearls.Contains(pearl.AbstractPearl.dataPearlType)) {
            AddCurrentCycleScore(self.oracle.room.game, MSC(20), ItemSymbol.SymbolDataFromItem(pearl.abstractPhysicalObject));
        }
    }

    private void SleepAndDeathScreen_GetDataFromGame(On.Menu.SleepAndDeathScreen.orig_GetDataFromGame orig, SleepAndDeathScreen self, KarmaLadderScreen.SleepDeathScreenDataPackage package)
    {
        orig(self, package);

        int tokens = self.endgameTokens?.tokens.Count ?? 0;

        Vector2 bottomLeft = new(self.LeftHandButtonsPosXAdd, 28 + 40 * Mathf.Ceil(tokens / 5f));

        int current = CurrentCycleScore;
        int total = GetTotalScore(package.saveState);
        int oldAverage = CurrentAverageScore;
        int newAverage = GetAverageScore(package.saveState);

        if (self.IsAnyDeath) {
            // Deaths are always -3. Time during failed cycles doesn't counted.
            current = -3;
        }

        self.pages[0].subObjects.Add(new ScoreTicker(self.pages[0], bottomLeft + new Vector2(0, 60), self.Translate("Cycle :")) {
            start = 0,
            end = current,
            fmtAdd = true,
            numberColor = ScoreTextColor(current, oldAverage)
        });

        self.pages[0].subObjects.Add(new ScoreTicker(self.pages[0], bottomLeft + new Vector2(0, 30), self.Translate("Total :")) {
            start = total - current,
            end = total,
            animationClock = -60,
        });

        self.pages[0].subObjects.Add(new ScoreTicker(self.pages[0], bottomLeft, self.Translate("Average :")) {
            start = oldAverage,
            end = newAverage,
            animationClock = -120,
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
        if (stats != null && stats.Checked && self.manager.rainWorld.progression.IsThereASavedGame(storyGameCharacter)) {
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

    private void SlugcatSelectMenu_ComingFromRedsStatistics(On.Menu.SlugcatSelectMenu.orig_ComingFromRedsStatistics orig, SlugcatSelectMenu self)
    {
        // Fixes a vanilla bug...
        orig(self);
        self.redIsDead = self.saveGameData.TryGetValue(SlugcatStats.Name.Red, out var s) && s != null && (s.redsDeath && s.cycle >= RedsIllness.RedsCycles(s.redsExtraCycles) || s.ascended);
    }

    private void StoryGameStatisticsScreen_AddBkgIllustration(On.Menu.StoryGameStatisticsScreen.orig_AddBkgIllustration orig, StoryGameStatisticsScreen self)
    {
        if (viewStats == null) {
            orig(self);
            return;
        }
        
        // Fix background illustration for living characters
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
