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

    float flash;
    float lastFlash;
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

        lastFlash = flash;
        flash = Custom.LerpAndTick(flash, 0f, 0.05f, 0.01f);

        bool fast = RWInput.PlayerInput(0, menu.manager.rainWorld).mp;

        animationClock += fast ? 4 : 1;

        if (animationClock > 0 && !visible) {
            visible = true;
            flash = 1f;
            UpdateText();
            menu.PlaySound(SoundID.UI_Multiplayer_Player_Result_Box_Kill_Tick);
        }

        if (animationClock > 40 && start != end && (fast || Math.Abs(end - start) > 10 || animationClock % 4 == 0)) {
            Tick();
        }
    }

    public void Tick()
    {
        start += Math.Sign(end - start);

        flash = Mathf.Max(0.75f, flash);

        UpdateText();

        menu.PlaySound(SoundID.UI_Multiplayer_Player_Result_Box_Number_Tick);
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        float flash = Mathf.Lerp(lastFlash, this.flash, timeStacker);

        Color numberColor = Color.Lerp(this.numberColor, Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White), Mathf.Pow(flash, 2f));
        Color textColor = Color.Lerp(Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey), Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White), Mathf.Pow(flash, 3f));

        numberLabel.label.isVisible = visible;
        nameLabel.label.isVisible = visible;

        numberLabel.label.color = this.numberColor == default ? textColor : numberColor;
        nameLabel.label.color = textColor;
    }
}
