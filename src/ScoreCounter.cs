using Menu;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ScoreGalore;

sealed class ScoreCounter : HUD.HudPart
{
    public sealed class ScoreBonus
    {
        public int Add;
        public Color Color;
        public FLabel label;
    }

    public int Score;
    public int TargetScore = 10;
    public int LastMinute;

    readonly int[] killScores;
    readonly List<ScoreBonus> bonuses = new();

    Vector2 pos;
    Vector2 lastPos;

    FLabel scoreText;
    FSprite darkGradient;
    FSprite lightGradient;
    float alpha;
    float lastAlpha;
    float bump;
    float lastBump;
    int remainVisible;
    int clock;

    int incrementDelay;
    int incrementCounter;

    public ScoreCounter(HUD.HUD hud) : base(hud)
    {
        // TODO set TargetScore to average score

        pos = new Vector2(hud.rainWorld.screenSize.x - 20f + 0.01f, 20.01f);

        hud.fContainers[0].AddChild(scoreText = new FLabel(Custom.GetDisplayFont(), "0"));

        hud.fContainers[0].AddChild(darkGradient = new("Futile_White", true) {
            color = new Color(0f, 0f, 0f),
            shader = hud.rainWorld.Shaders["FlatLight"]
        });

        hud.fContainers[0].AddChild(lightGradient = new("Futile_White", true) {
            shader = hud.rainWorld.Shaders["FlatLight"]
        });

        killScores = new int[ExtEnum<MultiplayerUnlocks.SandboxUnlockID>.values.Count];
        for (int i = 0; i < killScores.Length; i++) {
            killScores[i] = 1;
        }
        SandboxSettingsInterface.DefaultKillScores(ref killScores);
        killScores[(int)MultiplayerUnlocks.SandboxUnlockID.Slugcat] = 1;
    }

    private Color ScoreTextColor() => new HSLColor(Custom.LerpMap(Score, 0f, TargetScore * 2f, 0f, 240f / 360f), 0.75f, 0.75f).rgb;

    private string FmtAdd(int add) => add > 0 ? "+" + add : add.ToString();

    private void RemoveCurrentBonus()
    {
        hud.fadeCircles.Add(new HUD.FadeCircle(hud, 10f, 10f, 0.82f, 30f, 4f, pos, hud.fContainers[1]));
        hud.PlaySound(SoundID.HUD_Food_Meter_Fill_Fade_Circle);
        bonuses[0].label.RemoveFromContainer();
        bonuses.RemoveAt(0);
    }

    public void AddBonus(ScoreBonus bonus)
    {
        if (bonus.Add == 0) return;

        incrementDelay = 0;
        incrementCounter = 0;
        bump = 1f;

        if (bonuses.FirstOrDefault(b => b.Color == bonus.Color) is ScoreBonus current) {
            current.Add += bonus.Add;
        }
        else {
            bonuses.Add(bonus);
        }
    }

    public void AddKill(Creature victim)
    {
        IconSymbol.IconSymbolData iconData = CreatureSymbol.SymbolDataFromCreature(victim.abstractCreature);

        var s = (StoryGameStatisticsScreen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StoryGameStatisticsScreen));
        var score = s.GetNonSandboxKillscore(victim.Template.type);

        if (score == 0 && MultiplayerUnlocks.SandboxUnlockForSymbolData(iconData) is MultiplayerUnlocks.SandboxUnlockID unlockID) {
            score = killScores[unlockID.Index];
        }

        if (score != 0) {
            bonuses.Add(new() { Add = score, Color = CreatureSymbol.ColorOfCreature(iconData) });
        }
    }

    public override void Update()
    {
        base.Update();

        lastPos = pos;
        lastAlpha = alpha;
        lastBump = bump;

        bump = Custom.LerpAndTick(bump, 0f, 0.04f, 0.033333335f);

        clock++;

        if (bonuses.Count > 0) {
            alpha = Custom.LerpAndTick(alpha, remainVisible > 0 ? 1f : Mathf.InverseLerp(10f, 50f, incrementDelay), 0.06f, 0.033333335f);

            Update(bonuses[0]);
        }
        else {
            alpha = Custom.LerpAndTick(alpha, remainVisible > 0 ? 1 : 0, 1f / 25f, 1f / 60f);

            incrementDelay = 0;
            incrementCounter = 0;
        }

        if (hud.owner.RevealMap) {
            remainVisible = Math.Max(remainVisible, 10);
        }
        else if (remainVisible > 0) {
            remainVisible--;
        }
    }

    private void Update(ScoreBonus bonus)
    {
        if (++incrementDelay < 80) {
            return;
        }

        if (++incrementCounter < 10) {
            return;
        }

        incrementCounter = 0;
        bump = 1f;
        remainVisible = 20;

        int add = Math.Sign(bonus.Add);

        Score += add;
        scoreText.text = Score.ToString();

        bonus.Add -= add;
        if (bonus.Add == 0) {
            RemoveCurrentBonus();
        }
    }

    public override void Draw(float timeStacker)
    {
        base.Draw(timeStacker);

        scoreText.color = ScoreTextColor();
        lightGradient.color = ScoreTextColor();

        Vector2 pos = Vector2.Lerp(lastPos, this.pos, timeStacker);
        scoreText.x = pos.x;
        scoreText.y = pos.y;

        bool blinkBright = bonuses.Count > 0 && incrementDelay % 16 < 8;

        float alpha = Mathf.Lerp(lastAlpha, this.alpha, timeStacker);
        scoreText.alpha = alpha * (blinkBright ? 1f : 0.5f);
        darkGradient.x = pos.x;
        darkGradient.y = pos.y;
        darkGradient.scale = Mathf.Lerp(35f, 40f, alpha) / 16f;

        float bump = Mathf.Lerp(lastBump, this.bump, timeStacker);
        darkGradient.alpha = 0.17f * Mathf.Pow(alpha, 2f) + 0.1f * bump * (blinkBright ? 1f : 0.5f);
        lightGradient.x = pos.x;
        lightGradient.y = pos.y;
        lightGradient.scale = Mathf.Lerp(40f, 50f, Mathf.Pow(bump, 2f)) / 16f;
        lightGradient.alpha = bump * 0.2f;

        int i = 0;
        foreach (var bonus in bonuses) {
            i++;

            if (bonus.label == null) {
                bonus.label = new(Custom.GetDisplayFont(), "");
                hud.fContainers[0].AddChild(bonus.label);
            }
            pos.x -= 30;
            bonus.label.scale = 0.8f;
            bonus.label.text = FmtAdd(bonus.Add);
            bonus.label.color = bonus.Color;
            bonus.label.alpha = Mathf.Min(alpha, 0.5f + 0.5f * Mathf.Sign(i + clock / Mathf.PI / 40f));
            bonus.label.x = pos.x;
            bonus.label.y = pos.y;
        }
    }

    public override void ClearSprites()
    {
        base.ClearSprites();
        scoreText.RemoveFromContainer();
    }
}
