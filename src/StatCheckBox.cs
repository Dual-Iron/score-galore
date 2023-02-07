using Menu;

namespace ScoreGalore;

sealed class StatCheckBox : CheckBox
{
    struct Simple : IOwnCheckBox
    {
        bool on;
        bool IOwnCheckBox.GetChecked(CheckBox box) => on;
        void IOwnCheckBox.SetChecked(CheckBox box, bool c) => on = c;
    }

    private readonly new SlugcatSelectMenu menu;

    public StatCheckBox(SlugcatSelectMenu menu, float posX) : base(menu, menu.pages[0], new Simple(), new(posX, -30), 40f, RWCustom.Custom.ToTitleCase(menu.Translate("STATISTICS").ToLower()), "", true)
    {
        this.menu = menu;
    }

    public override void Clicked()
    {
        base.Clicked();

        if (Checked) {
            menu.startButton.fillTime = 40f;
            menu.startButton.menuLabel.text = menu.Translate("STATISTICS");
        }
        else {
            menu.UpdateStartButtonText();
        }
    }

    public override void Update()
    {
        SlugcatStats.Name name = menu.colorFromIndex(menu.slugcatPageIndex);

        bool hidden = menu.saveGameData[name] == null || name == SlugcatStats.Name.Red && menu.redIsDead;

        GetButtonBehavior.greyedOut = hidden || menu.restartChecked;
        selectable = !(hidden || menu.restartChecked);
        pos.y = hidden ? -30 : 30;

        base.Update();

        if (menu.restartChecked) {
            Checked = false;
        }
    }
}
