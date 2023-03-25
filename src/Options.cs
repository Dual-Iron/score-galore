using Menu.Remix.MixedUI;
using UnityEngine;

namespace ScoreGalore;

sealed class Options : OptionInterface
{
    public static Configurable<float> KillDelay;
    public static Configurable<bool> ShowRealTime;
    public static Configurable<bool> ShowSleepScreen;

    public Options()
    {
        KillDelay = config.Bind("cfgKillDelay", 5f, new ConfigAcceptableRange<float>(0, 15f));
        ShowRealTime = config.Bind("cfgShowRealTime", true);
        ShowSleepScreen = config.Bind("cfgShowSleepScreen", true);
    }

    public override void Initialize()
    {
        base.Initialize();

        Tabs = new OpTab[] { new OpTab(this) };

        float y = 300;

        var author = new OpLabel(20, 600 - 40, "by Dual", true);
        var github = new OpLabel(20, 600 - 40 - 40, "github.com/Dual-Iron/score-galore");

        var s0 = new OpLabel(new(300, y), Vector2.zero, "Delay before showing kills, in seconds", FLabelAlignment.Right);
        var c0 = new OpFloatSlider(KillDelay, new(344, y - 6), 220);

        var d1 = new OpLabel(new(300, y -= 50), Vector2.zero, "Show score in real-time", FLabelAlignment.Right);
        var c1 = new OpCheckBox(ShowRealTime, new(332, y - 4));

        var d2 = new OpLabel(new(300, y -= 50), Vector2.zero, "Show score on sleep screen", FLabelAlignment.Right);
        var c2 = new OpCheckBox(ShowSleepScreen, new(332, y - 4));

        Tabs[0].AddItems(author, github, s0, c0, d1, c1, d2, c2);
    }
}
