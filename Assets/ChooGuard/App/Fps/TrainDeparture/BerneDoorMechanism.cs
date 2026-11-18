namespace ChooGuard.App.Fps.TrainDeparture
{
    public sealed class BerneDoorMechanism
    {
        public float Depth { get; private set; }
        public float Rotation { get; private set; }
        public bool CloseCommand => buttonHeld && Depth >= 1 && Rotation >= 1 && !Retrieved;
        public bool ClosedLamp { get; private set; }
        public bool Retrieved { get; private set; }
        bool buttonHeld, lampObserved;
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static float Clamp(float n) => System.Math.Max(0, System.Math.Min(1, n));
        readonly DepartureGuardedOperation<float> insert, rotate, withdraw;
        readonly DepartureGuardedOperation<bool> press, observe;
        public BerneDoorMechanism()
        {
            // Conditions/effects are cached once; physical samples allocate no dispatch objects.
            insert = new DepartureGuardedOperation<float>(
                amount => Finite(amount) && amount > 0 && !Retrieved && !lampObserved,
                amount => Depth = Clamp(Depth + amount));
            rotate = new DepartureGuardedOperation<float>(
                amount => Finite(amount) && Depth >= 1 && !Retrieved && !buttonHeld,
                amount => Rotation = Clamp(Rotation + amount));
            press = new DepartureGuardedOperation<bool>(
                held => held && Depth >= 1 && Rotation >= 1 && !Retrieved,
                held => buttonHeld = true);
            observe = new DepartureGuardedOperation<bool>(
                _ => ClosedLamp && CloseCommand,
                _ => lampObserved = true);
            withdraw = new DepartureGuardedOperation<float>(
                amount => Finite(amount) && amount > 0 && ClosedLamp && lampObserved && !buttonHeld && !Retrieved,
                amount =>
                {
                    Depth = Clamp(Depth - amount);
                    if (Depth == 0) { Rotation = 0; Retrieved = true; }
                });
        }
        public void Insert(float amount) { insert.TryApply(amount); }
        public void Rotate(float amount) { rotate.TryApply(amount); }
        public void SetButtonHeld(bool held)
        {
            // Release (including an invalid press) must always clear a previous command.
            buttonHeld = false;
            press.TryApply(held);
        }
        public void ReadDoorFeedback(int closed, int expected)
        {
            ClosedLamp = expected > 0 && closed == expected;
            if (!ClosedLamp) lampObserved = false;
        }
        public bool TryObserveLamp() => observe.TryApply(true);
        public void ObserveLamp() { TryObserveLamp(); } // Existing callers retain their public API.
        public bool Withdraw(float amount) => withdraw.TryApply(amount);
        public void Reset()
        {
            Depth = Rotation = 0;
            buttonHeld = lampObserved = ClosedLamp = Retrieved = false;
        }
    }
}
