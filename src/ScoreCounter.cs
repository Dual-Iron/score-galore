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
    }

    public int Score;
    public int AverageScore = 10;

    readonly int[] killScores;
    readonly List<ScoreBonus> bonuses = new();

    Vector2 pos;
    Vector2 lastPos;

    public FLabel scoreText;
    public FSprite darkGradient;
    public FSprite lightGradient;
    public float alpha;
    public float lastAlpha;
    public float bump;
    public float lastBump;
    public int remainVisible;
    public int lastMinute;

    public int incrementDelay;
    public int incrementCounter;

    public ScoreCounter(HUD.HUD hud) : base(hud)
    {
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

    private Color ScoreTextColor => new HSLColor(Custom.LerpMap(Score, 0f, AverageScore * 2f, 0f, 240f / 360f), 0.75f, 0.75f).rgb;

    public void AddBonus(ScoreBonus bonus)
    {
        if (bonus.Add == 0) return;

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

        if (bonuses.FirstOrDefault() is ScoreBonus bonus) {
            alpha = Custom.LerpAndTick(alpha, remainVisible > 0 ? 1f : Mathf.InverseLerp(10f, 50f, incrementDelay), 0.06f, 0.033333335f);

            Update(bonus);
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

        Score += Math.Sign(bonus.Add);
        bonus.Add -= Math.Sign(bonus.Add);
        scoreText.text = Score.ToString();

        if (bonus.Add == 0) {
            hud.fadeCircles.Add(new HUD.FadeCircle(hud, 10f, 10f, 0.82f, 30f, 4f, pos, hud.fContainers[1]));
            hud.PlaySound(SoundID.HUD_Food_Meter_Fill_Fade_Circle);

            bonuses.Remove(bonus);
        }
    }

    public override void Draw(float timeStacker)
    {
        base.Draw(timeStacker);

        scoreText.color = ScoreTextColor;
        lightGradient.color = ScoreTextColor;

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
    }

    public override void ClearSprites()
    {
        base.ClearSprites();
        scoreText.RemoveFromContainer();
    }
}
