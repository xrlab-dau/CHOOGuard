using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // Scene-authored links between the independent controls and their physical equipment.
    public sealed class DepartureEquipment : MonoBehaviour
    {
        public TrainDepartureGame Game;
        public DepartureManipulation Manipulation;
        public DepartureManipulable CrewHandle, CrewDoor;
        public DepartureManipulable[] KeypadKeys;
        [Tooltip("Fictional demonstration code. Never enter a real crew credential.")]
        public string DemonstrationCode="1234";
        public Renderer Led;
        public Material LedOpen, LedClosed;
        public Renderer Bogie;
        public Material[] BogieStopped, BogieDeparture;
        public Renderer[] KeypadLamps;
        public Material KeypadIdle, KeypadAccepted;
        string entered="";
        bool bound, ready, closed;
        void OnEnable()
        {
            if(bound)return;
            if(Game==null||CrewHandle==null)return;
            Game.Restarted+=ResetEquipment;
            CrewHandle.StrokeCompleted.AddListener(ReleaseLatch);
            if(KeypadKeys!=null)foreach(var key in KeypadKeys)if(key!=null)key.DigitPressed.AddListener(EnterDigit);
            bound=true;ResetEquipment();
        }
        void ReleaseLatch(){if(!CrewHandle.Unlocked)return;if(CrewDoor!=null)CrewDoor.SetHandleReleased(true);else Game.BeginLuggageStow();}
        void EnterDigit(int digit)
        {
            entered+=digit.ToString();
            if(DemonstrationCode==null||DemonstrationCode.Length==0){entered="";return;}
            if(entered.Length>=DemonstrationCode.Length)
            {
                bool accepted=entered==DemonstrationCode;
                CrewHandle.SetUnlocked(accepted);CrewDoor.SetUnlocked(accepted);
                if(!accepted)entered="";
            }
            ApplyKeypad();
        }
        void ApplyKeypad()
        {
            if(KeypadLamps==null)return;
            for(int i=0;i<KeypadLamps.Length;i++)if(KeypadLamps[i]!=null)
                KeypadLamps[i].sharedMaterial=i<entered.Length?KeypadAccepted:KeypadIdle;
        }
        void ResetEquipment()
        {
            entered="";ready=closed=false;
            if(Led!=null&&LedOpen!=null)Led.sharedMaterial=LedOpen;
            if(Bogie!=null&&BogieStopped!=null&&BogieStopped.Length>0)Bogie.sharedMaterials=BogieStopped;
            ApplyKeypad();
        }
        void LateUpdate()
        {
            if(Game==null)return;
            bool nextClosed=Game.Mechanism.ClosedLamp;
            bool nextReady=Game.Flow.Stage>=DepartureStage.ReceiveAnnouncement;
            if(nextClosed!=closed){closed=nextClosed;if(Led!=null)Led.sharedMaterial=closed?LedClosed:LedOpen;}
            if(nextReady!=ready){ready=nextReady;if(Bogie!=null)Bogie.sharedMaterials=ready?BogieDeparture:BogieStopped;}
        }
        void OnDisable()
        {
            if(!bound)return;
            if(Game!=null)Game.Restarted-=ResetEquipment;
            if(CrewHandle!=null)CrewHandle.StrokeCompleted.RemoveListener(ReleaseLatch);
            if(KeypadKeys!=null)foreach(var key in KeypadKeys)if(key!=null)key.DigitPressed.RemoveListener(EnterDigit);
            bound=false;
        }
    }
}
