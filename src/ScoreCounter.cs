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

    public int Score {
        get => Plugin.CurrentCycleScore;
        set => Plugin.CurrentCycleScore = value;
    }
    public int TargetScore => Plugin.CurrentAverageScore;

    readonly List<ScoreBonus> bonuses = new();
    readonly FLabel scoreText;
    readonly FSprite darkGradient;
    readonly FSprite lightGradient;

    Vector2 pos;
    Vector2 lastPos;

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
        pos = new Vector2(hud.rainWorld.screenSize.x - 20f + 0.01f, 20.01f);

        hud.fContainers[0].AddChild(scoreText = new FLabel(Custom.GetDisplayFont(), "0"));

        hud.fContainers[0].AddChild(darkGradient = new("Futile_White", true) {
            color = new Color(0f, 0f, 0f),
            shader = hud.rainWorld.Shaders["FlatLight"]
        });

        hud.fContainers[0].AddChild(lightGradient = new("Futile_White", true) {
            shader = hud.rainWorld.Shaders["FlatLight"]
        });
    }

    private void RemoveCurrentBonus()
    {
        hud.fadeCircles.Add(new HUD.FadeCircle(hud, 10f, 10f, 0.82f, 30f, 4f, pos, hud.fContainers[1]));
        hud.PlaySound(SoundID.HUD_Food_Meter_Fill_Fade_Circle);
        bonuses[0].label.RemoveFromContainer();
        bonuses.RemoveAt(0);
    }

    public void AddBonus(ScoreBonus bonus)
    {
        incrementDelay = 0;
        incrementCounter = 0;
        bonuses.Add(bonus);
    }

    public override void Update()
    {
        base.Update();

        lastPos = pos;
        lastAlpha = alpha;
        lastBump = bump;

        bump = Custom.LerpAndTick(bump, 0f, 1f/25f, 1f/30f);

        clock++;

        if (bonuses.Count > 0) {
            alpha = Custom.LerpAndTick(alpha, 1f, 1f/17f, 1f/30f);

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

        if (scoreText.text != Score.ToString()) {
            scoreText.text = Score.ToString();
        }
    }

    private void Update(ScoreBonus bonus)
    {
        if (++incrementDelay < 160) {
            return;
        }

        if (++incrementCounter < 5) {
            return;
        }

        incrementCounter = 0;
        bump = 1f;
        remainVisible = 40;

        int add = Math.Sign(bonus.Add);

        Score += add;

        bonus.Add -= add;
        if (bonus.Add == 0) {
            RemoveCurrentBonus();
        }
    }

    public override void Draw(float timeStacker)
    {
        base.Draw(timeStacker);

        scoreText.color = Plugin.ScoreTextColor(Score, TargetScore);
        lightGradient.color = Plugin.ScoreTextColor(Score, TargetScore);

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
            bonus.label.text = Plugin.FmtAdd(bonus.Add);
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
