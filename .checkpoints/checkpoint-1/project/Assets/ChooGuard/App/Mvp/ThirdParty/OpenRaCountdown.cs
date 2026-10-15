#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 *
 * Adapted for CHOOGuard: engine-independent accepted-simulation-time wrapper.
 * Source: OpenRA.Mods.Common/Activities/Wait.cs
 * Commit: f3ec7f8e1593b482f85fd101652deb740c33dee6
 */
#endregion
using System;

namespace ChooGuard.App.Mvp.ThirdParty
{
    /// <summary>OpenRA Wait's post-decrement activity, driven only by accepted time.</summary>
    public sealed class OpenRaCountdown
    {
        private int remainingTicks;
        private bool isCanceling;
        private double fractionalMilliseconds;
        public bool Complete { get; private set; }
        public float TotalSeconds { get; }
        public float RemainingSeconds=>Complete?0:(float)Math.Max(0,(remainingTicks+1-fractionalMilliseconds)/1000d);

        public OpenRaCountdown(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>86400)throw new ArgumentOutOfRangeException(nameof(seconds));
            remainingTicks=(int)Math.Ceiling((double)seconds*1000);
            TotalSeconds=remainingTicks/1000f;
            // Original Wait(N) returns true on invocation N+1, not N.
            // Prime invocation 1 here; N accepted millisecond ticks remain.
            Complete=Tick();
        }
        private bool Tick()
        {
            // Actual OpenRA Wait cancellation + post-decrement completion port.
            if(isCanceling)return true;
            return remainingTicks-- == 0;
        }
        public void Cancel() { isCanceling=true;Complete=Tick(); }
        public bool AdvanceAcceptedSeconds(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(Complete)return true;
            fractionalMilliseconds+=(double)seconds*1000;
            while(fractionalMilliseconds>=1 && !Complete) { fractionalMilliseconds-=1;Complete=Tick(); }
            return Complete;
        }
    }
}
