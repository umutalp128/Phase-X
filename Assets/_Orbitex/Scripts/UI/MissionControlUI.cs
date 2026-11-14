using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The full dark-themed Mission Control screen, built entirely in code:
/// top bar (scenario select), left (segment role tabs + component catalog),
/// right (trade-off telemetry + requirement checks + launch), bottom (educational advisor),
/// plus briefing / results modals.
/// </summary>
public class MissionControlUI : MonoBehaviour
{
    MissionCalculator _calc;
    RectTransform _root;

    // top bar
    Text _missionTitle, _missionSub;
    readonly List<(MissionScenario sc, Image img)> _scenarioButtons = new List<(MissionScenario, Image)>();

    // left
    readonly List<(MissionSegmentRole role, Image img, Text txt)> _roleTabs = new List<(MissionSegmentRole, Image, Text)>();
    Text _roleDesc;
    RectTransform _listContent;
    ScrollRect _listScroll;
    readonly List<PartCard> _cards = new List<PartCard>();

    // right
    readonly Dictionary<string, TelemetryRow> _rows = new Dictionary<string, TelemetryRow>();
    RectTransform _checksContent;
    Button _launchButton;

    // bottom
    Text _tipTitle, _tipBody, _tipTag;

    // modal
    RectTransform _modal;

    class PartCard
    {
        public SpacecraftPart Part;
        public Image Bg;
        public Text Status;
        public bool Hover;
    }

    class TelemetryRow
    {
        public Text Value;
        public RectTransform Fill;
        public Image FillImg;
    }

    public void Init(MissionCalculator calc)
    {
        _calc = calc;
        BuildCanvas();
        _calc.OnScenarioChanged += HandleScenarioChanged;
        _calc.OnRoleChanged += HandleRoleChanged;
        _calc.OnDesignChanged += RefreshDesign;
        _calc.OnMissionLaunched += HandleLaunched;
        HandleScenarioChanged();
    }

    void OnDestroy()
    {
        if (_calc == null) return;
        _calc.OnScenarioChanged -= HandleScenarioChanged;
        _calc.OnRoleChanged -= HandleRoleChanged;
        _calc.OnDesignChanged -= RefreshDesign;
        _calc.OnMissionLaunched -= HandleLaunched;
    }

    // ================================================================== build

    void BuildCanvas()
    {
        var canvasGo = new GameObject("[Orbitex] MissionControlCanvas", typeof(RectTransform));
        canvasGo.layer = 5;
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)canvasGo.transform;

        BuildTopBar();
        BuildLeftPanel();
        BuildRightPanel();
        BuildBottomBar();
    }

    void BuildTopBar()
    {
        var bar = OrbitexUI.Panel(_root, "TopBar", OrbitexUI.BarBg);
        bar.rectTransform.Top(0, 72);
        var line = OrbitexUI.Panel(bar.transform, "Line", OrbitexUI.Accent, false);
        line.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));

        var badge = OrbitexUI.Panel(bar.transform, "Badge", OrbitexUI.NasaRed, false);
        badge.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, -18), new Vector2(54, 18));
        badge.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var inner = OrbitexUI.Panel(badge.transform, "Inner", OrbitexUI.BarBg, false);
        inner.rectTransform.Fill(7);

        var title = OrbitexUI.Label(bar.transform, "ORBITEX", 34, OrbitexUI.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        title.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(72, 18), new Vector2(260, -4));
        var sub = OrbitexUI.Label(bar.transform, "LUNAR MISSION  //  MISSION DESIGN LAB", 13, OrbitexUI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
        sub.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(74, 6), new Vector2(420, 26));

        _missionTitle = OrbitexUI.Label(bar.transform, "", 24, OrbitexUI.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        _missionTitle.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-250, 26), new Vector2(250, -4));
        FitOneLine(_missionTitle, 14);
        _missionSub = OrbitexUI.Label(bar.transform, "", 14, OrbitexUI.Dim, TextAnchor.MiddleCenter);
        _missionSub.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-250, 4), new Vector2(250, 26));
        FitOneLine(_missionSub, 10);

        var group = OrbitexUI.Rect("ScenarioButtons", bar.transform);
        group.Anchor(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-700, 14), new Vector2(-14, -14));
        var h = group.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        string[] labels = { "L1  CUBESAT", "L2  ICE HUNTER", "L3  FAR SIDE", "SANDBOX" };
        var scenarios = OrbitexDatabase.Scenarios;
        for (int i = 0; i < scenarios.Count; i++)
        {
            var sc = scenarios[i];
            var b = OrbitexUI.Button(group, i < labels.Length ? labels[i] : sc.Title, OrbitexUI.Card, OrbitexUI.Text,
                () => _calc.LoadScenario(sc), 14);
            _scenarioButtons.Add((sc, b.GetComponent<Image>()));
            var hover = b.gameObject.AddComponent<OrbitexHover>();
            hover.Enter = () => ShowTip(sc.ConceptMaturityLabel + "  -  " + sc.Title, sc.Briefing.Split('\n')[0], "SCENARIO");
            hover.Exit = ShowDefaultTip;
        }
        var brief = OrbitexUI.Button(group, "BRIEFING", new Color(0.12f, 0.2f, 0.3f), OrbitexUI.Accent, ShowBriefing, 14);
        OrbitexUI.Layout(brief.gameObject, -1, 110, 0);
    }

    void BuildLeftPanel()
    {
        var panel = OrbitexUI.Panel(_root, "LeftPanel", OrbitexUI.Bg);
        panel.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(12, 108), new Vector2(482, -84));

        var header = OrbitexUI.Label(panel.transform, "MISSION SEGMENTS", 16, OrbitexUI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
        header.rectTransform.Top(10, 26, 16, 16);

        var tabs = OrbitexUI.Rect("RoleTabs", panel.transform).Top(42, 96, 14, 14);
        var grid = tabs.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(216, 44);
        grid.spacing = new Vector2(10, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        var order = new[] { MissionSegmentRole.LaunchSegment, MissionSegmentRole.SpaceSegment, MissionSegmentRole.UserSegment, MissionSegmentRole.GroundSegment };
        foreach (var role in order)
        {
            var r = role;
            var b = OrbitexUI.Button(tabs, OrbitexDatabase.RoleTitle(r) + " SEGMENT", OrbitexUI.Card, OrbitexUI.Text,
                () => _calc.SetActiveRole(r), 15);
            _roleTabs.Add((r, b.GetComponent<Image>(), OrbitexUI.ButtonText(b)));
        }

        _roleDesc = OrbitexUI.Label(panel.transform, "", 14, OrbitexUI.Dim, TextAnchor.UpperLeft);
        _roleDesc.rectTransform.Top(146, 48, 16, 16);

        var sep = OrbitexUI.Panel(panel.transform, "Sep", OrbitexUI.Line, false);
        sep.rectTransform.Top(198, 1, 14, 14);

        var listArea = OrbitexUI.Rect("ListArea", panel.transform);
        listArea.Anchor(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-6, -206));
        _listContent = OrbitexUI.ScrollList(listArea, out _listScroll);
        ((RectTransform)_listScroll.transform).Fill();
    }

    void BuildRightPanel()
    {
        var panel = OrbitexUI.Panel(_root, "RightPanel", OrbitexUI.Bg);
        panel.rectTransform.Anchor(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-462, 108), new Vector2(-12, -84));

        var header = OrbitexUI.Label(panel.transform, "TRADE-OFF TELEMETRY", 16, OrbitexUI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
        header.rectTransform.Top(10, 26, 16, 16);

        var rows = OrbitexUI.Rect("Telemetry", panel.transform).Top(42, 8 * 44, 16, 16);
        OrbitexUI.VList(rows.gameObject, 4);
        AddRow(rows, "budget", "BUDGET");
        AddRow(rows, "launch", "LAUNCH MASS");
        AddRow(rows, "bus", "BUS LOAD");
        AddRow(rows, "power", "POWER USE");
        AddRow(rows, "data", "DATA vs DOWNLINK");
        AddRow(rows, "dv", "DELTA-V");
        AddRow(rows, "science", "SCIENCE");
        AddRow(rows, "rel", "RELIABILITY");

        var reqHeader = OrbitexUI.Label(panel.transform, "REQUIREMENTS", 16, OrbitexUI.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
        reqHeader.rectTransform.Top(42 + 8 * 44 + 8, 26, 16, 16);

        _checksContent = OrbitexUI.Rect("Checks", panel.transform);
        _checksContent.Anchor(Vector2.zero, Vector2.one, new Vector2(16, 92), new Vector2(-16, -(42 + 8 * 44 + 40)));
        OrbitexUI.VList(_checksContent.gameObject, 2);

        var clear = OrbitexUI.Button(panel.transform, "CLEAR", OrbitexUI.Card, OrbitexUI.Dim, () => _calc.ClearDesign(), 15);
        ((RectTransform)clear.transform).Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(116, 76));
        _launchButton = OrbitexUI.Button(panel.transform, "LAUNCH MISSION", OrbitexUI.NasaRed, Color.white, () => _calc.LaunchMission(), 22);
        ((RectTransform)_launchButton.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(126, 16), new Vector2(-16, 76));
        var hover = _launchButton.gameObject.AddComponent<OrbitexHover>();
        hover.Enter = () => ShowTip("Launch Review", "Submits your design to the mission review board. All enabled requirements must pass for a successful mission.", "REVIEW");
        hover.Exit = ShowDefaultTip;
    }

    void AddRow(Transform parent, string key, string label)
    {
        var row = OrbitexUI.Rect("Row_" + key, parent);
        OrbitexUI.Layout(row.gameObject, 40);
        var l = OrbitexUI.Label(row, label, 13, OrbitexUI.Dim, TextAnchor.UpperLeft, FontStyle.Bold);
        l.rectTransform.Top(0, 20);
        var v = OrbitexUI.Label(row, "-", 14, OrbitexUI.Text, TextAnchor.UpperRight, FontStyle.Bold);
        v.rectTransform.Top(0, 20);
        OrbitexUI.Bar(row, out var fill);
        var barRt = (RectTransform)fill.parent;
        barRt.Top(23, 10);
        _rows[key] = new TelemetryRow { Value = v, Fill = fill, FillImg = fill.GetComponent<Image>() };
    }

    void BuildBottomBar()
    {
        var bar = OrbitexUI.Panel(_root, "AdvisorBar", OrbitexUI.BarBg);
        bar.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 96));
        var line = OrbitexUI.Panel(bar.transform, "Line", OrbitexUI.Line, false);
        line.rectTransform.Top(0, 2);

        var tagBg = OrbitexUI.Panel(bar.transform, "Tag", new Color(0.1f, 0.22f, 0.3f), false);
        tagBg.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, -16), new Vector2(178, 16));
        _tipTag = OrbitexUI.Label(tagBg.transform, "ADVISOR", 13, OrbitexUI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        _tipTag.rectTransform.Fill();

        _tipTitle = OrbitexUI.Label(bar.transform, "", 19, OrbitexUI.Text, TextAnchor.UpperLeft, FontStyle.Bold);
        _tipTitle.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(200, 50), new Vector2(-24, -14));
        _tipBody = OrbitexUI.Label(bar.transform, "", 16, OrbitexUI.Dim, TextAnchor.UpperLeft);
        _tipBody.rectTransform.Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(200, 6), new Vector2(-24, -44));
    }

    // ================================================================== refresh

    void HandleScenarioChanged()
    {
        var sc = _calc.CurrentScenario;
        _missionTitle.text = sc.Title.ToUpperInvariant();
        _missionSub.text = $"{sc.ConceptMaturityLabel}   |   Budget {MissionCalculator.Money(sc.MaxBudget)}   |   Science target {sc.TargetScience:0} pts";
        foreach (var (s, img) in _scenarioButtons)
            img.color = s == sc ? new Color(0.1f, 0.38f, 0.5f) : OrbitexUI.Card;
        HandleRoleChanged();
        RefreshDesign();
        ShowDefaultTip();
        ShowBriefing();
    }

    void HandleRoleChanged()
    {
        foreach (var (role, img, txt) in _roleTabs)
        {
            bool on = role == _calc.ActiveRole;
            img.color = on ? new Color(0.1f, 0.38f, 0.5f) : OrbitexUI.Card;
            txt.color = on ? Color.white : OrbitexUI.Dim;
        }
        _roleDesc.text = OrbitexDatabase.RoleDescription(_calc.ActiveRole);
        RebuildList();
    }

    void RebuildList()
    {
        if (_listContent == null) return;
        OrbitexUI.Clear(_listContent);
        _cards.Clear();
        var role = _calc.ActiveRole;
        foreach (var group in OrbitexDatabase.PartsFor(role).GroupBy(p => p.Category))
        {
            var cat = group.Key;
            string mode = MissionCalculator.IsSingleChoice(cat) ? "pick one" : "pick any";
            var head = OrbitexUI.Label(_listContent, OrbitexDatabase.CategoryTitle(cat) + "  " + OrbitexUI.Col("(" + mode + ")", OrbitexUI.Dim),
                14, OrbitexUI.CategoryColor(cat), TextAnchor.LowerLeft, FontStyle.Bold);
            OrbitexUI.Layout(head.gameObject, 26);
            foreach (var part in group) BuildCard(part);
        }
        _listScroll.verticalNormalizedPosition = 1f;
        RefreshCards();
    }

    void BuildCard(SpacecraftPart part)
    {
        var bg = OrbitexUI.Panel(_listContent, "Card_" + part.Id, OrbitexUI.Card);
        OrbitexUI.Layout(bg.gameObject, 92);
        var card = new PartCard { Part = part, Bg = bg };

        var stripe = OrbitexUI.Panel(bg.transform, "Stripe", OrbitexUI.CategoryColor(part.Category), false);
        stripe.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(5, 0));

        var name = OrbitexUI.Label(bg.transform, part.Name, 17, OrbitexUI.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        name.rectTransform.Top(7, 24, 16, 112);
        FitOneLine(name, 12);
        card.Status = OrbitexUI.Label(bg.transform, "", 12, OrbitexUI.Dim, TextAnchor.UpperRight, FontStyle.Bold);
        card.Status.rectTransform.Top(10, 20, 200, 12);
        var stats = OrbitexUI.Label(bg.transform, StatsLine(part), 13, OrbitexUI.Dim, TextAnchor.UpperLeft);
        stats.rectTransform.Top(34, 22, 16, 12);
        var stats2 = OrbitexUI.Label(bg.transform, StatsLine2(part), 13, OrbitexUI.Dim, TextAnchor.UpperLeft);
        stats2.rectTransform.Top(56, 22, 16, 12);

        var btn = bg.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            if (!_calc.IsUnlocked(part))
            {
                ShowTip(part.Name + "  (locked)", $"Unlocks at {OrbitexDatabase.CmlName(part.UnlockLevel)}. " + part.EducationalTooltip, "LOCKED");
                return;
            }
            _calc.TogglePart(part);
        });
        var hover = bg.gameObject.AddComponent<OrbitexHover>();
        hover.Enter = () => { card.Hover = true; RefreshCard(card); ShowPartTip(part); };
        hover.Exit = () => { card.Hover = false; RefreshCard(card); ShowDefaultTip(); };
        _cards.Add(card);
    }

    static string StatsLine(SpacecraftPart p)
    {
        var bits = new List<string> { OrbitexUI.Col(MissionCalculator.Money(p.CostMillionUSD), OrbitexUI.Warn) };
        if (p.MassKg > 0) bits.Add($"{p.MassKg:0.#} kg");
        if (p.PowerWatts > 0) bits.Add(OrbitexUI.Col($"+{p.PowerWatts:0} W", OrbitexUI.Ok));
        if (p.PowerWatts < 0) bits.Add(OrbitexUI.Col($"{p.PowerWatts:0} W", OrbitexUI.Bad));
        bits.Add($"rel {p.Reliability * 100f:0.#}%");
        return string.Join("   ", bits);
    }

    static string StatsLine2(SpacecraftPart p)
    {
        var bits = new List<string>();
        switch (p.Category)
        {
            case PartCategory.LaunchVehicle: bits.Add($"Lifts {p.MaxPayloadCapacityKg:0} kg"); break;
            case PartCategory.Structure: bits.Add($"Carries {p.MaxPayloadCapacityKg:0} kg of parts"); break;
            case PartCategory.Propulsion: bits.Add($"{p.DeltaV:0} m/s for {p.MaxPayloadCapacityKg:0} kg craft"); break;
            case PartCategory.Orbit: bits.Add(p.DeltaV > 0 ? $"Needs +{p.DeltaV:0} m/s" : "No extra delta-V"); break;
            case PartCategory.Comms: bits.Add($"Downlink {p.DataRateMbps:0.##} Mbps"); break;
            case PartCategory.Payload: bits.Add($"Makes {p.DataRateMbps:0.##} Mbps"); break;
        }
        if (p.ScienceScore > 0) bits.Add(OrbitexUI.Col($"+{p.ScienceScore:0} science", OrbitexUI.Accent));
        if (p.IsSolar) bits.Add("solar");
        return string.Join("   ", bits);
    }

    void RefreshCards()
    {
        foreach (var c in _cards) RefreshCard(c);
    }

    void RefreshCard(PartCard c)
    {
        if (c.Bg == null) return;
        bool unlocked = _calc.IsUnlocked(c.Part);
        bool sel = _calc.IsSelected(c.Part);
        c.Bg.color = !unlocked ? OrbitexUI.CardLocked : sel ? OrbitexUI.CardSelected : c.Hover ? OrbitexUI.CardHover : OrbitexUI.Card;
        if (!unlocked)
            c.Status.text = OrbitexUI.Col("LOCKED  CML " + c.Part.UnlockLevel, OrbitexUI.Dim);
        else if (sel)
            c.Status.text = OrbitexUI.Col("SELECTED", OrbitexUI.Ok);
        else
            c.Status.text = OrbitexUI.Col(c.Hover ? "CLICK TO ADD" : "", OrbitexUI.Accent);
        foreach (var t in c.Bg.GetComponentsInChildren<Text>())
            if (t != c.Status) t.canvasRenderer.SetAlpha(unlocked ? 1f : 0.45f);
    }

    void RefreshDesign()
    {
        if (_calc == null || _rows.Count == 0) return;
        RefreshCards();
        var r = _calc.LastReport;
        var sc = _calc.CurrentScenario;

        SetUsage("budget", r.TotalCost, r.Budget, $"{MissionCalculator.Money(r.TotalCost)} / {MissionCalculator.Money(r.Budget)}");
        SetUsage("launch", r.SpacecraftMassKg, r.LaunchCapacityKg,
            r.LaunchCapacityKg > 0 ? $"{r.SpacecraftMassKg:0.#} / {r.LaunchCapacityKg:0} kg" : $"{r.SpacecraftMassKg:0.#} kg / no launcher");
        SetUsage("bus", r.ComponentMassKg, r.BusCapacityKg,
            r.BusCapacityKg > 0 ? $"{r.ComponentMassKg:0.#} / {r.BusCapacityKg:0} kg" : $"{r.ComponentMassKg:0.#} kg / no bus");
        SetUsage("power", r.PowerConsumed, r.PowerGenerated,
            $"{r.PowerConsumed:0} / {r.PowerGenerated:0} W" + (sc.SolarEfficiency < 1f ? $"  (solar {sc.SolarEfficiency * 100f:0}%)" : ""));
        SetUsage("data", r.DataGenerated, r.DataDownlink, $"{r.DataGenerated:0.##} / {r.DataDownlink:0.##} Mbps", !sc.CheckDataBottleneck);
        SetGoal("dv", r.DeltaVAvailable, r.DeltaVRequired, $"{r.DeltaVAvailable:0} / {r.DeltaVRequired:0} m/s", !sc.CheckDeltaV);
        SetGoal("science", r.Science, r.TargetScience, $"{r.Science:0} / {r.TargetScience:0} pts");
        SetGoal("rel", r.Reliability, Mathf.Max(0.01f, sc.MinReliability), $"{r.Reliability * 100f:0.0}%  (min {sc.MinReliability * 100f:0}%)", false, r.Reliability);

        OrbitexUI.Clear(_checksContent);
        foreach (var c in r.Checks)
        {
            var row = OrbitexUI.Rect("Check", _checksContent);
            OrbitexUI.Layout(row.gameObject, 24);
            string badge = !c.Enabled ? OrbitexUI.Col("[ -- ]", OrbitexUI.Dim) : c.Passed ? OrbitexUI.Col("[PASS]", OrbitexUI.Ok) : OrbitexUI.Col("[FAIL]", OrbitexUI.Bad);
            var l = OrbitexUI.Label(row, badge + "  " + c.Name, 14, c.Enabled ? OrbitexUI.Text : OrbitexUI.Dim, TextAnchor.MiddleLeft, FontStyle.Bold);
            l.rectTransform.Fill();
            var d = OrbitexUI.Label(row, c.Enabled ? c.Detail : "unlocks at higher CML", 12, OrbitexUI.Dim, TextAnchor.MiddleRight);
            d.rectTransform.Anchor(Vector2.zero, Vector2.one, new Vector2(170, 0), Vector2.zero);
            FitOneLine(d, 9);
        }

        OrbitexUI.ButtonText(_launchButton).text = r.MissionReady ? "LAUNCH MISSION" : "LAUNCH (NOT READY)";
        _launchButton.GetComponent<Image>().color = r.MissionReady ? OrbitexUI.NasaRed : new Color(0.35f, 0.16f, 0.16f);
    }

    static void FitOneLine(Text t, int minSize)
    {
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = minSize;
        t.resizeTextMaxSize = t.fontSize;
        t.verticalOverflow = VerticalWrapMode.Truncate;
    }

    /// <summary>Lower is better (resource use against a limit).</summary>
    void SetUsage(string key, float used, float limit, string text, bool disabled = false)
    {
        var row = _rows[key];
        float ratio = limit > 0 ? used / limit : used > 0 ? 2f : 0f;
        Color c = disabled ? OrbitexUI.Dim : ratio > 1f ? OrbitexUI.Bad : ratio > 0.85f ? OrbitexUI.Warn : OrbitexUI.Ok;
        row.Fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
        row.FillImg.color = c;
        row.Value.text = OrbitexUI.Col(text, disabled ? OrbitexUI.Dim : c == OrbitexUI.Ok ? OrbitexUI.Text : c);
    }

    /// <summary>Higher is better (achievement against a target).</summary>
    void SetGoal(string key, float have, float need, string text, bool disabled = false, float? fillOverride = null)
    {
        var row = _rows[key];
        float ratio = need > 0 ? have / need : 1f;
        Color c = disabled ? OrbitexUI.Dim : ratio >= 1f ? OrbitexUI.Ok : ratio >= 0.75f ? OrbitexUI.Warn : OrbitexUI.Bad;
        row.Fill.anchorMax = new Vector2(Mathf.Clamp01(fillOverride ?? ratio), 1);
        row.FillImg.color = c;
        row.Value.text = OrbitexUI.Col(text, disabled ? OrbitexUI.Dim : c == OrbitexUI.Ok ? OrbitexUI.Text : c);
    }

    // ================================================================== advisor tips

    void ShowTip(string title, string body, string tag = "ADVISOR")
    {
        _tipTag.text = tag;
        _tipTitle.text = title;
        _tipBody.text = body;
    }

    void ShowPartTip(SpacecraftPart p)
    {
        string single = MissionCalculator.IsSingleChoice(p.Category) ? " (replaces your current pick)" : "";
        ShowTip(p.Name + OrbitexUI.Col("   " + OrbitexDatabase.CategoryTitle(p.Category) + single, OrbitexUI.Dim),
            p.EducationalTooltip, OrbitexDatabase.RoleTitle(p.Role) + " SEG");
    }

    void ShowDefaultTip()
    {
        var r = _calc.LastReport;
        var firstFail = r.Checks.FirstOrDefault(c => c.Enabled && !c.Passed);
        if (_calc.SelectedParts.Count == 0)
            ShowTip("Welcome, Mission Architect.",
                "Pick a segment tab on the left, then click components to add them. Hover any component to learn its trade-off. Drag the spacecraft to rotate it.");
        else if (firstFail != null)
            ShowTip("Next issue: " + firstFail.Name, r.Advice.FirstOrDefault() ?? firstFail.Detail);
        else
            ShowTip(OrbitexUI.Col("All requirements met!", OrbitexUI.Ok),
                "Your design is ready for launch review. Press LAUNCH MISSION - or optimise cost, science and reliability for more stars.");
    }

    // ================================================================== modals

    RectTransform OpenModal(string title, string subtitle, out RectTransform body, out RectTransform buttons, float height = 600)
    {
        CloseModal();
        var dim = OrbitexUI.Panel(_root, "Modal", new Color(0, 0.01f, 0.03f, 0.78f));
        dim.rectTransform.Fill();
        _modal = dim.rectTransform;

        var card = OrbitexUI.Panel(dim.transform, "Card", new Color(0.05f, 0.075f, 0.12f, 0.99f));
        card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460, -height / 2), new Vector2(460, height / 2));
        var accent = OrbitexUI.Panel(card.transform, "Accent", OrbitexUI.NasaRed, false);
        accent.rectTransform.Top(0, 4);

        var t = OrbitexUI.Label(card.transform, title, 34, OrbitexUI.Text, TextAnchor.UpperLeft, FontStyle.Bold);
        t.rectTransform.Top(26, 44, 40, 40);
        var s = OrbitexUI.Label(card.transform, subtitle, 16, OrbitexUI.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
        s.rectTransform.Top(74, 24, 40, 40);

        body = OrbitexUI.Rect("Body", card.transform);
        body.Anchor(Vector2.zero, Vector2.one, new Vector2(40, 96), new Vector2(-40, -112));

        buttons = OrbitexUI.Rect("Buttons", card.transform);
        buttons.Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 26), new Vector2(-40, 82));
        var h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14;
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        return card.rectTransform;
    }

    void CloseModal()
    {
        if (_modal != null) Destroy(_modal.gameObject);
        _modal = null;
    }

    Button ModalButton(RectTransform parent, string label, Color bg, Color fg, System.Action onClick, float width = 240)
    {
        var b = OrbitexUI.Button(parent, label, bg, fg, onClick, 18);
        OrbitexUI.Layout(b.gameObject, -1, width);
        return b;
    }

    void ShowBriefing()
    {
        var sc = _calc.CurrentScenario;
        OpenModal("MISSION BRIEFING", sc.ConceptMaturityLabel.ToUpperInvariant() + "   |   " + sc.Title.ToUpperInvariant(), out var body, out var buttons);

        var text = OrbitexUI.Label(body, sc.Briefing, 18, OrbitexUI.Text);
        text.rectTransform.Top(0, 170);

        var objectives = new List<string>
        {
            $"Budget cap: {OrbitexUI.Col(MissionCalculator.Money(sc.MaxBudget), OrbitexUI.Warn)}",
            $"Science target: {OrbitexUI.Col(sc.TargetScience.ToString("0") + " pts", OrbitexUI.Accent)}",
        };
        if (sc.AllowedOrbitIds != null && sc.AllowedOrbitIds.Length > 0)
            objectives.Add("Destination: " + OrbitexUI.Col(string.Join(" / ", sc.AllowedOrbitIds.Select(id => OrbitexDatabase.GetPart(id)?.Name ?? id)), OrbitexUI.Accent));
        if (sc.CheckDeltaV) objectives.Add($"Delta-V: mission needs {sc.RequiredDeltaV:0} m/s + orbit insertion");
        if (sc.CheckDataBottleneck) objectives.Add("Data: your radio must keep up with your instruments");
        if (sc.SolarEfficiency < 1f) objectives.Add($"Sunlight: solar panels at {sc.SolarEfficiency * 100f:0}%");
        objectives.Add($"Reliability: at least {sc.MinReliability * 100f:0}%");
        if (sc.EnableCrisisEvents) objectives.Add(OrbitexUI.Col("Crisis events: ENABLED (coming in a later phase)", OrbitexUI.Dim));

        var obj = OrbitexUI.Label(body, OrbitexUI.Col("OBJECTIVES", OrbitexUI.Dim) + "\n- " + string.Join("\n- ", objectives), 16, OrbitexUI.Text);
        obj.rectTransform.Anchor(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -175));

        ModalButton(buttons, "BEGIN DESIGN", OrbitexUI.NasaRed, Color.white, CloseModal, 260);
    }

    void HandleLaunched(MissionReport report)
    {
        StopAllCoroutines();
        StartCoroutine(ShowResultsDelayed(report));
    }

    IEnumerator ShowResultsDelayed(MissionReport r)
    {
        ShowTip(r.MissionReady ? "Launch sequence initiated..." : "Review board is checking your design...", "Stand by for the mission review.", "REVIEW");
        yield return new WaitForSeconds(1.2f);
        ShowResults(r);
    }

    void ShowResults(MissionReport r)
    {
        var sc = _calc.CurrentScenario;
        string title = r.MissionReady ? "MISSION SUCCESS" : "MISSION NOT READY";
        var card = OpenModal(title, $"{sc.Title.ToUpperInvariant()}   |   SCORE {r.Score:0} / 100", out var body, out var buttons, 640);

        // Stars (rotated squares; no font glyphs needed)
        var stars = OrbitexUI.Rect("Stars", card);
        stars.Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -70), new Vector2(-40, -30));
        for (int i = 0; i < 3; i++)
        {
            var st = OrbitexUI.Panel(stars, "Star", i < r.Stars ? new Color(1f, 0.8f, 0.25f) : new Color(0.18f, 0.22f, 0.3f), false);
            st.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18 + i * 48, -13), new Vector2(44 + i * 48, 13));
            st.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        }

        var lines = new List<string>
        {
            $"Cost {OrbitexUI.Col(MissionCalculator.Money(r.TotalCost), OrbitexUI.Warn)} of {MissionCalculator.Money(r.Budget)}     " +
            $"Science {OrbitexUI.Col(r.Science.ToString("0"), OrbitexUI.Accent)} / {r.TargetScience:0}     " +
            $"Reliability {OrbitexUI.Col((r.Reliability * 100f).ToString("0.0") + "%", OrbitexUI.Ok)}",
            ""
        };
        var failed = r.Checks.Where(c => c.Enabled && !c.Passed).ToList();
        if (failed.Count > 0)
        {
            lines.Add(OrbitexUI.Col("FAILED REQUIREMENTS", OrbitexUI.Bad));
            lines.AddRange(failed.Select(c => $"- {c.Name}: {OrbitexUI.Col(c.Detail, OrbitexUI.Dim)}"));
            lines.Add("");
        }
        else
        {
            lines.Add(OrbitexUI.Col("All requirements passed. The review board approves your mission!", OrbitexUI.Ok));
            lines.Add("");
        }
        if (r.Advice.Count > 0)
        {
            lines.Add(OrbitexUI.Col("SYSTEMS ENGINEER'S NOTES", OrbitexUI.Accent));
            lines.AddRange(r.Advice.Take(4).Select(a => "- " + a));
        }
        var text = OrbitexUI.Label(body, string.Join("\n", lines), 17, OrbitexUI.Text);
        text.rectTransform.Fill();

        ModalButton(buttons, "BACK TO DESIGN", OrbitexUI.Card, OrbitexUI.Text, () => { CloseModal(); ShowDefaultTip(); });
        var next = _calc.NextScenario();
        if (r.MissionReady && next != null)
            ModalButton(buttons, "NEXT: " + (next.IsSandbox ? "SANDBOX" : "CML " + next.DifficultyLevel), OrbitexUI.NasaRed, Color.white,
                () => _calc.LoadScenario(next), 260);
    }
}
