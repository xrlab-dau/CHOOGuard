// Adapted from UnityHFSM v2.3.0 f8fdc2efb862522acd26fab6cd940886c84cde37.
// https://github.com/Inspiaaa/UnityHFSM/tree/f8fdc2efb862522acd26fab6cd940886c84cde37
// Transition.cs 35-44: condition/optional callback; StateMachine.cs 361-365:
// guard before commit. T is the domain command context instead of transition-this;
// the accepted effect replaces RequestStateChange. No hierarchy/scheduler is copied.
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

namespace ChooGuard.App.Fps.TrainDeparture
{
    internal sealed class DepartureGuardedOperation<T>
    {
        readonly Func<T, bool> condition;
        readonly Action<T> beforeTransition;
        internal DepartureGuardedOperation(Func<T, bool> condition, Action<T> effect)
        { this.condition = condition; beforeTransition = effect; }
        bool ShouldTransition(T context)
        {
            if (condition == null) return true;
            return condition(context);
        }
        void BeforeTransition(T context) => beforeTransition?.Invoke(context);
        internal bool TryApply(T context)
        {
            if (!ShouldTransition(context)) return false;
            BeforeTransition(context);
            return true;
        }
    }
}
