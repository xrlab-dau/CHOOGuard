// Bounded source adaptation from UnityHFSM v2.3.0, commit
// f8fdc2efb862522acd26fab6cd940886c84cde37 (Inspiaaa and contributors).
// https://github.com/Inspiaaa/UnityHFSM/tree/f8fdc2efb862522acd26fab6cd940886c84cde37
// Source -> local symbol: StateMachine.cs 45-59 -> StateBundle.AddTriggerTransition;
// StateMachine.cs 733-744 -> TryTrigger active-state loop; 361-365 -> TryTransition;
// Transition.cs 35-41 -> Transition.ShouldTransition; TransitionBase.cs 9-10, 44-45
// -> Transition.from/to and constructor assignments. Local modifications narrow IDs
// to departure enums, omit hierarchy/exit-time/global transitions, and use MoveTo
// for synchronous domain notification. Registrations and observation flags are local.
/*
MIT License

Copyright (c) 2020 Inspiaaa

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Collections.Generic;

namespace ChooGuard.App.Fps.TrainDeparture
{
    public enum DepartureStage
    {
        WaitForArrival, BoardForLuggage, EnterCrewSpace, StowLuggage, ExitForInspection,
        InspectExterior, ReboardForCrewReport, ReceiveCrewReport, ExitForSignal,
        SignalCaptain, ReceiveAnnouncement, ReboardForDoorControl, CloseDoors,
        ObserveClosedLamp, RetrieveKey, SendDepartureRadio, Complete
    }
    public enum DepartureAction
    {
        TrainStopped, EnterThirdCoach, EnterCrewSpace, PlaceOwnBag, ExitThirdCoach,
        InspectExterior, InspectAirbag, InspectBogie, CrewInspectionReport,
        SignalCaptain, CaptainAnnouncement, AllDoorsClosed, ObserveChangedLamp,
        RetrieveKey, TransmitDeparture
    }
    public sealed class DepartureFlow
    {
        // Only the upstream guarded-trigger subset is retained; no Unity/frame dependency.
        sealed class Transition
        {
            public readonly DepartureStage from;
            public readonly DepartureStage to;
            readonly Func<Transition, bool> condition;

            public Transition(DepartureStage from, DepartureStage to, Func<Transition, bool> condition = null)
            {
                this.from = from;
                this.to = to;
                this.condition = condition;
            }

            public bool ShouldTransition()
            {
                if (condition == null)
                    return true;

                return condition(this);
            }
        }

        sealed class StateBundle
        {
            public Dictionary<DepartureAction, List<Transition>> triggerToTransitions;

            public void AddTriggerTransition(DepartureAction trigger, Transition transition)
            {
                triggerToTransitions = triggerToTransitions
                    ?? new Dictionary<DepartureAction, List<Transition>>();

                List<Transition> transitionsOfTrigger;

                if (!triggerToTransitions.TryGetValue(trigger, out transitionsOfTrigger))
                {
                    transitionsOfTrigger = new List<Transition>();
                    triggerToTransitions.Add(trigger, transitionsOfTrigger);
                }

                transitionsOfTrigger.Add(transition);
            }
        }

        readonly Dictionary<DepartureStage, StateBundle> stateToTransitions =
            new Dictionary<DepartureStage, StateBundle>();

        public DepartureStage Stage { get; private set; }
        public bool AirbagObserved { get; private set; }
        public bool ExteriorObserved { get; private set; }
        public bool BogieObserved { get; private set; }
        public event System.Action<DepartureStage> StageChanged;

        public DepartureFlow()
        {
            Stage = DepartureStage.WaitForArrival;
            AddTriggerTransition(DepartureAction.TrainStopped,
                new Transition(DepartureStage.WaitForArrival, DepartureStage.BoardForLuggage));
            AddTriggerTransition(DepartureAction.EnterThirdCoach,
                new Transition(DepartureStage.BoardForLuggage, DepartureStage.EnterCrewSpace));
            // Compatibility identifiers: this step is the KTXP_03126 handle, not room creation.
            AddTriggerTransition(DepartureAction.EnterCrewSpace,
                new Transition(DepartureStage.EnterCrewSpace, DepartureStage.StowLuggage));
            AddTriggerTransition(DepartureAction.PlaceOwnBag,
                new Transition(DepartureStage.StowLuggage, DepartureStage.ExitForInspection));
            AddTriggerTransition(DepartureAction.ExitThirdCoach,
                new Transition(DepartureStage.ExitForInspection, DepartureStage.InspectExterior));
            AddTriggerTransition(DepartureAction.InspectAirbag,
                new Transition(DepartureStage.InspectExterior, DepartureStage.ReboardForCrewReport, AllInspectionsObserved));
            AddTriggerTransition(DepartureAction.InspectExterior,
                new Transition(DepartureStage.InspectExterior, DepartureStage.ReboardForCrewReport, AllInspectionsObserved));
            AddTriggerTransition(DepartureAction.InspectBogie,
                new Transition(DepartureStage.InspectExterior, DepartureStage.ReboardForCrewReport, AllInspectionsObserved));
            AddTriggerTransition(DepartureAction.EnterThirdCoach,
                new Transition(DepartureStage.ReboardForCrewReport, DepartureStage.ReceiveCrewReport));
            AddTriggerTransition(DepartureAction.CrewInspectionReport,
                new Transition(DepartureStage.ReceiveCrewReport, DepartureStage.ExitForSignal));
            AddTriggerTransition(DepartureAction.ExitThirdCoach,
                new Transition(DepartureStage.ExitForSignal, DepartureStage.SignalCaptain));
            AddTriggerTransition(DepartureAction.SignalCaptain,
                new Transition(DepartureStage.SignalCaptain, DepartureStage.ReceiveAnnouncement));
            AddTriggerTransition(DepartureAction.CaptainAnnouncement,
                new Transition(DepartureStage.ReceiveAnnouncement, DepartureStage.ReboardForDoorControl));
            AddTriggerTransition(DepartureAction.EnterThirdCoach,
                new Transition(DepartureStage.ReboardForDoorControl, DepartureStage.CloseDoors));
            AddTriggerTransition(DepartureAction.AllDoorsClosed,
                new Transition(DepartureStage.CloseDoors, DepartureStage.ObserveClosedLamp));
            AddTriggerTransition(DepartureAction.ObserveChangedLamp,
                new Transition(DepartureStage.ObserveClosedLamp, DepartureStage.RetrieveKey));
            AddTriggerTransition(DepartureAction.RetrieveKey,
                new Transition(DepartureStage.RetrieveKey, DepartureStage.SendDepartureRadio));
            AddTriggerTransition(DepartureAction.TransmitDeparture,
                new Transition(DepartureStage.SendDepartureRadio, DepartureStage.Complete));
        }

        void AddTriggerTransition(DepartureAction trigger, Transition transition)
        {
            StateBundle bundle;
            if (!stateToTransitions.TryGetValue(transition.from, out bundle))
            {
                bundle = new StateBundle();
                stateToTransitions.Add(transition.from, bundle);
            }
            bundle.AddTriggerTransition(trigger, transition);
        }

        bool AllInspectionsObserved(Transition transition)
        {
            return AirbagObserved && ExteriorObserved && BogieObserved;
        }

        bool TryTransition(Transition transition)
        {
            if (!transition.ShouldTransition())
                return false;

            MoveTo(transition.to);
            return true;
        }

        bool TryTrigger(DepartureAction trigger)
        {
            StateBundle bundle;
            if (!stateToTransitions.TryGetValue(Stage, out bundle))
                return false;
            var activeTriggerTransitions = bundle.triggerToTransitions;
            List<Transition> triggerTransitions;

            if (activeTriggerTransitions.TryGetValue(trigger, out triggerTransitions))
            {
                for (int i = 0, count = triggerTransitions.Count; i < count; i++)
                {
                    Transition transition = triggerTransitions[i];

                    if (TryTransition(transition))
                        return true;
                }
            }

            return false;
        }

        public bool TryAction(DepartureAction action)
        {
            bool observed = false;
            if (Stage == DepartureStage.InspectExterior)
            {
                if (action == DepartureAction.InspectAirbag && !AirbagObserved) AirbagObserved = true;
                else if (action == DepartureAction.InspectExterior && !ExteriorObserved) ExteriorObserved = true;
                else if (action == DepartureAction.InspectBogie && !BogieObserved) BogieObserved = true;
                else return false;
                observed = true;
            }
            // Always evaluate the guarded trigger after recording a distinct observation.
            return TryTrigger(action) || observed;
        }
        void MoveTo(DepartureStage stage) { Stage = stage; StageChanged?.Invoke(stage); }
        public void Reset()
        {
            AirbagObserved = ExteriorObserved = BogieObserved = false;
            MoveTo(DepartureStage.WaitForArrival);
        }
    }
}
