// Adapted from deeming-games/unity-fps-asset WeaponSwitcher.cs, commit
// e0974630b914d9ecb6e31e726039cceaa69b9610, StartSwitchingWeapon/CompleteSwitchingWeapon.
// Retains accepted-next, holster-before-draw and commit/clear-next sequencing.
// Domain adapter adds one-item guards, completion polling, same-digit toggle and cancellation.
/*
MIT License

Copyright (c) 2021 deeming-games

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
namespace ChooGuard.App.Fps.TrainDeparture
{
    internal interface IDepartureSlotItems
    {
        bool CanSwitch { get; }
        bool SocketLocked { get; }
        bool Available(int slot);
        bool Stowed(int slot);
        bool Drawn(int slot);
        bool StartStow(int slot);
        bool StartDraw(int slot);
        void CancelEquip();
    }

    internal sealed class DepartureSlotSelection
    {
        readonly IDepartureSlotItems items;
        readonly DepartureGuardedOperation<int> request;
        int? nextSlot;
        bool drawing;
        public int SelectedSlot { get; private set; } = 3;
        public bool IsSwitching => nextSlot.HasValue;
        public int RequestedSlot => nextSlot ?? SelectedSlot;
        public DepartureSlotSelection(IDepartureSlotItems items)
        {
            this.items = items;
            request = new DepartureGuardedOperation<int>(
                slot => slot >= 1 && slot <= 4 && items.CanSwitch && !items.SocketLocked && !IsSwitching && items.Available(slot),
                StartSwitching);
        }
        public bool RequestSlot(int slot)
        {
            if (!request.TryApply(slot)) return false;
            return IsSwitching || SelectedSlot == 4 && slot == 4;
        }
        void StartSwitching(int slot)
        {
            nextSlot = slot == SelectedSlot ? 4 : slot;
            drawing = false;
            if (!items.Stowed(SelectedSlot) && !items.StartStow(SelectedSlot)) { nextSlot = null; return; }
            Step();
        }
        public void Step()
        {
            // A socketed key is world equipment, never a carried selection or hide target.
            if (items.SocketLocked) { SelectedSlot = 4; nextSlot = null; drawing = false; return; }
            if (!IsSwitching)
            {
                if (!items.Available(SelectedSlot) || items.Stowed(SelectedSlot)) SelectedSlot = 4;
                return;
            }
            if (!items.CanSwitch) { Cancel(); return; }
            if (!drawing)
            {
                if (!items.Stowed(SelectedSlot)) return;
                SelectedSlot = 4;
                if (nextSlot == 4) { nextSlot = null; return; }
                if (!items.Available(nextSlot.Value) || !items.StartDraw(nextSlot.Value)) { nextSlot = null; return; }
                drawing = true;
            }
            if (!items.Drawn(nextSlot.Value)) return;
            SelectedSlot = nextSlot.Value;
            nextSlot = null; drawing = false;
        }
        // Physical depth-zero cancellation returns the socket's same key to its owner.
        public void ObserveReturnedKey()
        {
            if (!IsSwitching && SelectedSlot == 4 && !items.SocketLocked && items.Drawn(1)) SelectedSlot = 1;
        }
        public void Cancel()
        {
            if (!IsSwitching) return;
            items.CancelEquip(); nextSlot = null; drawing = false; SelectedSlot = 4;
        }
        public void Reset()
        { nextSlot = null; drawing = false; SelectedSlot = 3; }
    }
}
