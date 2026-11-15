using System.Collections.Generic;
using System.Linq;
using PhaseX.Player;
using PhaseX.UI;
using UnityEngine;

namespace PhaseX.Station
{
    /// <summary>
    /// Wall-mounted NASA mission briefing smart-board. [E] opens a browser for the scenarios (A/D or the buttons to
    /// cycle, ENTER to accept): objective, requirements and the real mission background behind each level.
    /// The board itself always shows the active mission.
    /// </summary>
    public class MissionBriefingBoard : MonoBehaviour, IInteractable
    {
        public TextMesh titleText;
        public TextMesh bodyText;

        MissionCalculator _calc;
        int _index;

        void Start()
        {
            _calc = MissionCalculator.Instance;
            if (_calc != null) _calc.OnScenarioChanged += RefreshBoard;
            StationProgress.OnChanged += RefreshBoard;      // a new mission record shows up on the wall at once
            RefreshBoard();
        }

        void OnDestroy()
        {
            if (_calc != null) _calc.OnScenarioChanged -= RefreshBoard;
            StationProgress.OnChanged -= RefreshBoard;
        }

        /// <summary>"3 stars  88 / 100" for a flown mission, or a hint that it is still to be flown.</summary>
        static string BestLine(MissionScenario sc)
        {
            int stars = StationProgress.BestStars(sc.Id);
            float score = StationProgress.BestScore(sc.Id);
            return stars > 0 || score > 0f
                ? StationKit.Col(StationProgress.StarText(stars), new Color(1f, 0.8f, 0.25f)) + $"  {score:0} / 100"
                : StationKit.Col("not flown yet", new Color(0.55f, 0.62f, 0.72f));
        }

        void RefreshBoard()
        {
            if (_calc == null || _calc.CurrentScenario == null) return;
            var sc = _calc.CurrentScenario;
            if (titleText != null)
            {
                titleText.text = sc.Title.ToUpperInvariant();
                StationKit.FitWidth(titleText, 0.26f, 4.1f);     // long mission titles shrink to stay on the board
            }
            if (bodyText != null)
                bodyText.text = StationKit.Col("OBJECTIVE", StationKit.Amber) + "\n" + StationKit.Wrap(sc.Objective, 46) + "\n\n" +
                                StationKit.Col($"BUDGET {MissionCalculator.Money(sc.MaxBudget)}    SCIENCE {sc.TargetScience:0} pts    LIFE {sc.RequiredLifetimeMonths:0} mo", StationKit.Cyan) + "\n" +
                                StationKit.Col("YOUR BEST  ", StationKit.Amber) + BestLine(sc);
        }

        public string GetInteractionPrompt() => "[E] Open NASA Mission Briefing Board";

        public void Interact(PhaseXPlayerController player)
        {
            var hud = StationHUD.Instance;
            if (hud == null || _calc == null) return;
            var list = PhaseXDatabase.Scenarios;
            _index = Mathf.Max(0, list.ToList().IndexOf(_calc.CurrentScenario));
            StationProgress.BoardChecked = true;
            ShowCurrent(hud);
        }

        void Cycle(int dir)
        {
            int n = PhaseXDatabase.Scenarios.Count;
            _index = ((_index + dir) % n + n) % n;
            StationAudio.Blip();
            ShowCurrent(StationHUD.Instance);
        }

        void ShowCurrent(StationHUD hud)
        {
            var list = PhaseXDatabase.Scenarios;
            var sc = list[_index];
            bool active = sc == _calc.CurrentScenario;
            var dim = new Color(0.55f, 0.62f, 0.72f);

            var req = new List<string>
            {
                $"Budget cap {MissionCalculator.Money(sc.MaxBudget)}", $"Science target {sc.TargetScience:0} pts",
                $"Lifetime {sc.RequiredLifetimeMonths:0} months", $"Reliability at least {sc.MinReliability * 100f:0}%"
            };
            if (sc.AllowedOrbitIds.Length > 0) req.Add("Destination: " + string.Join(" / ", sc.AllowedOrbitIds.Select(i => PhaseXDatabase.GetPart(i)?.Name ?? i)));
            if (sc.SolarEfficiency < 1f) req.Add($"Solar panels work at {sc.SolarEfficiency * 100f:0}% here");

            string body =
                StationKit.Col("OBJECTIVE   ", StationKit.Amber) + sc.Objective + "\n\n" +
                sc.Briefing + "\n\n" +
                StationKit.Col("REQUIREMENTS   ", StationKit.Amber) + string.Join("   |   ", req) + "\n\n" +
                (PhaseXRuntimeUI.RecommendedInstruments(sc).Length > 0
                    ? StationKit.Col("RECOMMENDED INSTRUMENTS   ", StationKit.Amber) + PhaseXRuntimeUI.RecommendedInstruments(sc) + "\n\n" : "") +
                StationKit.Col("REAL MISSIONS   ", StationKit.Cyan) + StationKnowledge.MissionBackground(sc.Id) + "\n\n" +
                StationKit.Col("YOUR BEST   ", StationKit.Amber) + BestLine(sc) + "\n\n" +
                (active ? StationKit.Col("This is your ACTIVE mission.", StationKit.Green)
                        : StationKit.Col("Accepting a different mission clears the current spacecraft design.", dim));

            hud.ShowPanel($"NASA MISSION BRIEFING   {_index + 1} / {list.Count}   {StationProgress.EarnedStars(StationProgress.BestStars(sc.Id))}", sc.Title.ToUpperInvariant(), body, StationKit.Cyan,
                new List<StationHUD.PanelButton>
                {
                    new StationHUD.PanelButton { Label = "<  PREVIOUS  [A]", Key = PhaseXInput.K.A, OnClick = () => Cycle(-1) },
                    new StationHUD.PanelButton { Label = "NEXT  [D]  >", Key = PhaseXInput.K.D, OnClick = () => Cycle(1) },
                    new StationHUD.PanelButton { Label = active ? "CONTINUE MISSION  [ENTER]" : "ACCEPT MISSION  [ENTER]", Key = PhaseXInput.K.Enter, Primary = true, OnClick = Accept },
                });
        }

        void Accept()
        {
            var sc = PhaseXDatabase.Scenarios[_index];
            if (sc != _calc.CurrentScenario) _calc.LoadScenario(sc);
            PhaseXRuntimeUI.DismissBriefing();   // the board is the briefing: the monitor opens straight on the design screen
            StationProgress.BoardChecked = true;
            var hud = StationHUD.Instance;
            hud.ClosePanel();
            hud.Toast($"Mission accepted: {sc.Title}. Head to the CAD Monitor to design the spacecraft.", StationKit.Cyan);
            StationAudio.Chime();
        }

        public void BuildVisuals()
        {
            var t = transform;
            // bevelled frame, recessed glowing "Screen" and the amber accent "Bar": one Blender model
            PhaseXModelLibrary.InstantiateModel("Station/station_briefing_board", t);
            StationKit.Blocker(t, "Solid", new Vector3(0, 0, -0.04f), new Vector3(4.6f, 2.7f, 0.1f));
            StationKit.Text(t, "Header", "NASA MISSION BRIEFING", new Vector3(2.05f, 1.14f, 0.05f), Vector3.forward, 0.13f, StationKit.Amber, TextAnchor.MiddleLeft, TextAlignment.Left);
            titleText = StationKit.Text(t, "Title", "MISSION", new Vector3(2.05f, 0.76f, 0.05f), Vector3.forward, 0.26f, StationKit.Cyan, TextAnchor.MiddleLeft, TextAlignment.Left);
            bodyText = StationKit.Text(t, "Body", "", new Vector3(2.05f, 0.48f, 0.05f), Vector3.forward, 0.15f, StationKit.TextC, TextAnchor.UpperLeft, TextAlignment.Left);
            StationKit.Text(t, "Hint", "[E] BROWSE MISSIONS", new Vector3(-2.05f, -1.1f, 0.05f), Vector3.forward, 0.12f, StationKit.Amber, TextAnchor.MiddleRight, TextAlignment.Right);
            // (the board faces local +Z, so the reader's left is local +X: left-aligned text sits at +X)
            StationKit.Blocker(t, "InteractVolume", new Vector3(0, 0, 0.3f), new Vector3(4.6f, 2.7f, 0.7f), true);
        }
    }
}
