using Menu;
using RWCustom;
using System;
using UnityEngine;

namespace ScoreGalore;

sealed class ScoreTicker : PositionedMenuObject
{
    public int animationClock;
    public Color numberColor;

    public int start;
    public int end;
    public bool fmtAdd;

    readonly MenuLabel numberLabel;
    readonly MenuLabel nameLabel;

    float showFlash;
    float lastShowFlash;
    bool visible;

    public ScoreTicker(MenuObject owner, Vector2 pos, string name) : base(owner.menu, owner, pos)
    {
        numberLabel = new MenuLabel(menu, this, "", new Vector2(100f, -10f), new Vector2(60f, 20f), bigText: true);
        numberLabel.label.alignment = FLabelAlignment.Right;
        numberLabel.label.alpha = 0.5f;
        numberLabel.pos.x += 50f;
        subObjects.Add(numberLabel);

        nameLabel = new MenuLabel(menu, this, name, new Vector2(0f, 0f), default, bigText: true);
        nameLabel.label.alignment = FLabelAlignment.Left;
        nameLabel.label.alpha = 0.4f;
        subObjects.Add(nameLabel);
    }

    void UpdateText()
    {
        numberLabel.text = fmtAdd ? Plugin.FmtAdd(start) : start.ToString();
    }

    public override void Update()
    {
        base.Update();

        lastShowFlash = showFlash;
        showFlash = Custom.LerpAndTick(showFlash, 0f, 0.08f, 0.1f);

        animationClock += RWInput.PlayerInput(0, menu.manager.rainWorld).mp ? 4 : 1;

        if (animationClock > 0 && !visible) {
            visible = true;
            showFlash = 1f;
            lastShowFlash = 1f;
            UpdateText();
            menu.PlaySound(SoundID.UI_Multiplayer_Player_Result_Box_Kill_Tick);
        }

        if (animationClock > 12) {
            Tick();
        }
    }

    public void Tick()
    {
        if (start == end) return;

        if (Mathf.Abs(start - end) < 15)
            start += Math.Sign(end - start);
        else
            start += Math.Sign(end - start) * 10;

        showFlash = Mathf.Max(0.75f, showFlash);

        UpdateText();

        menu.PlaySound(SoundID.UI_Multiplayer_Player_Result_Box_Number_Tick);
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        Color grey = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
        float flash = Mathf.Lerp(lastShowFlash, showFlash, timeStacker);

        Color numberColor = Color.Lerp(this.numberColor, Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White), Mathf.Pow(flash, 2f));
        Color textColor = Color.Lerp(grey, Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White), Mathf.Pow(flash, 3f));

        numberLabel.label.isVisible = visible;
        nameLabel.label.isVisible = visible;

        numberLabel.label.color = numberColor == default ? textColor : numberColor;
        nameLabel.label.color = textColor;
    }
}
