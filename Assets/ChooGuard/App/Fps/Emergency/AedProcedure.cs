using System;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// What an automated external defibrillator does once it stands beside a patient, as the device itself runs it and as the
    /// lay rescuer works it (2026-10-31 user's order of operations): the lid opens (power on), each pad is taken out of the lid
    /// and stuck on the bare chest, the rescuer takes their hands off and calls "물러나세요", the device reads the rhythm, says
    /// "제세동이 필요합니다" and wants its button pressed — or says "전기충격이 필요없습니다" — and either way chest compressions go
    /// on until it asks everyone off again two minutes later. The device's voice is the only instruction (SIM_CONTROLS_BENCHMARK
    /// §6.8 step 5; 2025 한국 심폐소생술 가이드라인). It knows nothing of Unity: the unit in the scene (<see cref="AedUnit"/>) feeds
    /// it time and the two facts it reads from the patient, and relays its voice and its log lines. Whether a shock is advised
    /// is the device's rule read from the patient (not breathing normally and not yet in the paramedics' hands), computed
    /// deterministically; what the shock does to the patient is not decided here (JEV judges).
    /// </summary>
    public sealed class AedProcedure
    {
        public enum Stage
        {
            /// <summary>Lid shut, switched off (in the cabinet, in hand, or set down and not yet opened).</summary>
            Off,
            /// <summary>Lid open, on; the pads (<see cref="PadsNeeded"/>) are to be taken from the lid and stuck on the bare chest.</summary>
            PadsWanted,
            /// <summary>Both pads on: everyone must get off the patient — the rescuer calls "물러나세요" — before it reads the rhythm.</summary>
            ClearWanted,
            /// <summary>Reading the rhythm: nobody may touch the patient, or it starts over.</summary>
            Analysing,
            /// <summary>"제세동이 필요합니다": charging, then the flashing button must be pressed with everyone clear.</summary>
            ShockAdvised,
            /// <summary>Chest compressions until it asks everyone off again (<see cref="CompressionCycleSeconds"/>).</summary>
            Compressions,
            /// <summary>The paramedics have the patient: the device says nothing more.</summary>
            HandedOver,
        }

        public const int PadsNeeded = 2;
        /// <summary>How long the rhythm analysis takes with nobody touching the patient.</summary>
        public const float AnalysisSeconds = 5f;
        /// <summary>Charging time before the shock button lights.</summary>
        public const float ChargeSeconds = 3f;
        /// <summary>Compression cycle between analyses (the guideline's two minutes).</summary>
        public const float CompressionCycleSeconds = 120f;

        public Stage Current { get; private set; } = Stage.Off;
        /// <summary>Pads stuck on the chest.</summary>
        public int Pads { get; private set; }
        /// <summary>A pad is out of the lid and in the rescuer's hand (not yet on the chest).</summary>
        public bool PadInHand { get; private set; }
        /// <summary>Pads out of the lid (on the chest or in hand).</summary>
        public int PadsTaken => Pads + (PadInHand ? 1 : 0);
        /// <summary>Rhythm analyses completed.</summary>
        public int Analyses { get; private set; }
        public int Shocks { get; private set; }
        /// <summary>Times the rescuer called everyone off the patient ("물러나세요") for an analysis.</summary>
        public int ClearCalls { get; private set; }
        /// <summary>What the last completed analysis said: true = shock advised; null before the first analysis.</summary>
        public bool? LastAdvice { get; private set; }
        /// <summary>The shock button is lit (charged) and may be pressed.</summary>
        public bool Charged => Current == Stage.ShockAdvised && charge >= ChargeSeconds;
        /// <summary>Seconds of the current analysis or compression cycle gone by.</summary>
        public float Elapsed => timer;

        /// <summary>The device's rule read from the patient: true when the rhythm calls for a shock.</summary>
        public Func<bool> Shockable;
        /// <summary>Somebody is touching the patient (compressions under way): the analysis cannot run, the shock may not be given.</summary>
        public Func<bool> HandsOn;
        /// <summary>The device speaks (one line).</summary>
        public event Action<string> Voice;
        /// <summary>A fact for the shift log.</summary>
        public event Action<string> Logged;

        private float timer, charge;
        private bool touchWarned, readySaid;

        /// <summary>What E on the case does now; empty when the case itself wants nothing from the hand (the pads and the patient are their own targets).</summary>
        public string Prompt
        {
            get
            {
                switch (Current)
                {
                    case Stage.Off: return "뚜껑 열기";
                    case Stage.ShockAdvised: return Charged ? "충격 버튼 누르기" : "";
                    default: return "";
                }
            }
        }

        /// <summary>Why E on the case does nothing now (what to do instead, in the device's own words), or null when <see cref="Prompt"/> is not empty.</summary>
        public string Refusal
        {
            get
            {
                switch (Current)
                {
                    case Stage.PadsWanted: return PadInHand ? "손에 든 패드를 환자 가슴에 붙이십시오 (환자에 E)" : "뚜껑 안의 패드를 집으십시오 (패드에 E) · " + Pads + "/" + PadsNeeded;
                    case Stage.ClearWanted: return "환자에게서 손을 떼고 \"물러나세요\"를 외치십시오 (환자에 E)";
                    case Stage.Analysing: return "분석 중 · 환자에게서 손을 떼십시오";
                    case Stage.ShockAdvised: return Charged ? null : "충전 중 · 환자에게서 떨어지십시오";
                    case Stage.Compressions: return "가슴압박을 계속하십시오 · " + Math.Max(0, (int)Math.Ceiling(CompressionCycleSeconds - timer)) + "초 뒤 재분석";
                    case Stage.HandedOver: return "구급대가 인계받았습니다";
                    default: return null;
                }
            }
        }

        /// <summary>How the device stands, as anyone beside it sees it.</summary>
        public string StateText
        {
            get
            {
                switch (Current)
                {
                    case Stage.Off: return "뚜껑 닫힘 · 꺼짐";
                    case Stage.PadsWanted: return PadsTaken == 0 ? "켜짐 · 패드 대기" : "패드 " + Pads + "/" + PadsNeeded + (PadInHand ? " · 1장 손에" : "");
                    case Stage.ClearWanted: return "패드 부착 · 물러나기 대기";
                    case Stage.Analysing: return "분석 중 · 손 떼기";
                    case Stage.ShockAdvised: return Charged ? "제세동 대기 · 버튼 깜빡임" : "충전 중";
                    case Stage.Compressions: return "가슴압박 중 · 재분석까지 " + Math.Max(0, (int)Math.Ceiling(CompressionCycleSeconds - timer)) + "초" + (Shocks > 0 ? " · 충격 " + Shocks + "회" : "");
                    default: return "구급대 인계";
                }
            }
        }

        /// <summary>The lid opens: the device switches on and asks for the pads.</summary>
        public bool OpenLid()
        {
            if (Current != Stage.Off) return false;
            Current = Stage.PadsWanted;
            Pads = 0;
            PadInHand = false;
            Logged?.Invoke("AED 뚜껑 열고 전원 켬");
            Voice?.Invoke("전원이 켜졌습니다. 환자의 상의를 벗기고, 패드를 꺼내 그림대로 맨 가슴에 붙이십시오.");
            return true;
        }

        /// <summary>Switched off (lifted away, lid shut): the pads come off; what was done stays counted.</summary>
        public void PowerOff()
        {
            if (Current == Stage.Off) return;
            Current = Stage.Off;
            Pads = 0;
            PadInHand = false;
            timer = charge = 0;
            Logged?.Invoke("AED 전원 끔");
        }

        /// <summary>A pad is taken out of the lid into the hand (one at a time, while pads are wanted).</summary>
        public bool TakePad()
        {
            if (Current != Stage.PadsWanted || PadInHand || Pads >= PadsNeeded) return false;
            PadInHand = true;
            return true;
        }

        /// <summary>The pad in hand goes back into the lid (put down without sticking it on).</summary>
        public bool ReturnPad()
        {
            if (!PadInHand) return false;
            PadInHand = false;
            return true;
        }

        /// <summary>The pad in hand is stuck on the chest. The second pad makes the device ask everyone off the patient.</summary>
        public bool AttachPad()
        {
            if (Current != Stage.PadsWanted || !PadInHand || Pads >= PadsNeeded) return false;
            PadInHand = false;
            Pads++;
            if (Pads < PadsNeeded)
            {
                Voice?.Invoke("패드 한 장을 오른쪽 빗장뼈 아래에 붙였습니다. 다른 패드를 꺼내 왼쪽 젖꼭지 아래 옆구리에 붙이십시오.");
                return true;
            }
            Logged?.Invoke("AED 패드 " + PadsNeeded + "장 부착");
            WantClear("패드가 부착되었습니다. 모두 환자에게서 물러나십시오.");
            return true;
        }

        private void WantClear(string line)
        {
            Current = Stage.ClearWanted;
            timer = 0;
            Voice?.Invoke(line);
        }

        /// <summary>
        /// The rescuer takes their hands off and calls "물러나세요": the analysis starts. The scene stops the compressions first
        /// (<see cref="IncidentDirector.StandClear"/>); should hands stay on, the analysis holds and says so.
        /// </summary>
        public bool StandClear()
        {
            if (Current != Stage.ClearWanted) return false;
            ClearCalls++;
            Logged?.Invoke("\"물러나세요\" 외침 · AED 분석 시작");
            BeginAnalysis();
            return true;
        }

        private void BeginAnalysis()
        {
            Current = Stage.Analysing;
            timer = 0;
            touchWarned = false;
            Voice?.Invoke("심장 리듬을 분석 중입니다. 환자에게서 떨어지십시오.");
        }

        public bool PressShock()
        {
            if (Current != Stage.ShockAdvised) return false;
            if (!Charged) { Voice?.Invoke("충전 중입니다. 잠시 기다리십시오."); return false; }
            if (HandsOn != null && HandsOn()) { Voice?.Invoke("환자에게서 모두 떨어지십시오."); return false; }
            Shocks++;
            Logged?.Invoke("AED 심장충격 " + Shocks + "회");
            Current = Stage.Compressions;
            timer = 0;
            Voice?.Invoke("전기충격을 가했습니다. 즉시 가슴압박을 계속하십시오.");
            return true;
        }

        /// <summary>The paramedics have the patient: the device goes quiet (its counts stay).</summary>
        public void HandOver()
        {
            if (Current == Stage.Off || Current == Stage.HandedOver) return;
            Current = Stage.HandedOver;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0 || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            switch (Current)
            {
                case Stage.Analysing:
                    if (HandsOn != null && HandsOn())
                    {
                        // 손이 닿아 있으면 분석이 되지 않는다: 시계를 되돌리고 한 번 더 말한다.
                        timer = 0;
                        if (!touchWarned) { touchWarned = true; Voice?.Invoke("분석이 중단되었습니다. 환자에게서 손을 떼십시오."); }
                        return;
                    }
                    timer += deltaSeconds;
                    if (timer < AnalysisSeconds) return;
                    Analyses++;
                    bool advise = Shockable != null && Shockable();
                    LastAdvice = advise;
                    timer = 0;
                    if (advise)
                    {
                        Current = Stage.ShockAdvised;
                        charge = 0;
                        readySaid = false;
                        Logged?.Invoke("AED 분석 " + Analyses + "회 · 심장충격 필요");
                        Voice?.Invoke("제세동이 필요합니다. 충전 중입니다. 환자에게서 모두 떨어지십시오.");
                    }
                    else
                    {
                        Current = Stage.Compressions;
                        Logged?.Invoke("AED 분석 " + Analyses + "회 · 심장충격 불필요");
                        Voice?.Invoke("전기충격이 필요없습니다. 즉시 가슴압박을 계속하십시오.");
                    }
                    return;
                case Stage.ShockAdvised:
                    charge += deltaSeconds;
                    if (charge >= ChargeSeconds && !readySaid) { readySaid = true; Voice?.Invoke("깜빡이는 충격 버튼을 누르십시오."); }
                    return;
                case Stage.Compressions:
                    timer += deltaSeconds;
                    if (timer >= CompressionCycleSeconds) WantClear("가슴압박을 멈추십시오. 리듬 분석을 위해 모두 환자에게서 물러나십시오.");
                    return;
            }
        }
    }
}
