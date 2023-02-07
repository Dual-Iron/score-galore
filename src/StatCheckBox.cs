using Menu;

namespace ScoreGalore;

sealed class StatCheckBox : CheckBox
{
    private readonly new SlugcatSelectMenu menu;

    public bool isChecked;

    public StatCheckBox(SlugcatSelectMenu menu, float posX) : base(menu, menu.pages[0], menu, new(posX, -30), 40f, RWCustom.Custom.ToTitleCase(menu.Translate("STATISTICS").ToLower()), "", true)
    {
        this.menu = menu;
    }

    public override void Clicked()
    {
        base.Clicked();

        isChecked = !isChecked;

        if (isChecked) {
            menu.startButton.fillTime = 40f;
            menu.startButton.menuLabel.text = menu.Translate("STATISTICS");
        }
        else {
            menu.UpdateStartButtonText();
        }
    }

    public override void Update()
    {
        base.Update();

        SlugcatStats.Name name = menu.colorFromIndex(menu.slugcatPageIndex);

        bool available = menu.saveGameData[name] is SlugcatSelectMenu.SaveGameData data && !(name == SlugcatStats.Name.Red && data.redsDeath);

        GetButtonBehavior.greyedOut = !available;
        selectable = available;
        pos.y = available ? 30 : -30;

        if (isChecked) {
            menu.restartChecked = false;
            menu.restartCheckbox.GetButtonBehavior.greyedOut = true;
        }
    }
}
