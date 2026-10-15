using System;

namespace ChooGuard.Seeded
{
    public sealed class DecisionBudget
    {
        public sealed class Reservation
        {
            public long Input {get;}
            public long Output {get;}
            internal bool Closed;
            internal readonly DecisionBudget Owner;
            internal Reservation(DecisionBudget owner,long input,long output){Owner=owner;Input=input;Output=output;}
        }
        public long MaxRequests=24,MaxInputTokens=24000,MaxOutputTokens=6000;
        public int MaxConcurrent=1;
        public double TimeoutSeconds=8;
        public long Requests {get;private set;}
        public long MeasuredInputTokens {get;private set;}
        public long MeasuredOutputTokens {get;private set;}
        public long ChargedInputTokens {get;private set;}
        public long ChargedOutputTokens {get;private set;}
        public long EstimatedInputTokens {get;private set;}
        public long ReservedInputTokens {get;private set;}
        public long ReservedOutputTokens {get;private set;}
        public int InFlight {get;private set;}
        public bool Overrun {get;private set;}
        public bool TryReserve(long input,long output,out Reservation reservation,out string reason)
        {
            reservation=null;reason="";
            if(input<0||output<0) {reason="invalid_reservation";return false;}
            if(MaxRequests<0||MaxInputTokens<0||MaxOutputTokens<0||MaxConcurrent<1)reason="invalid_budget_limits";
            else if(Overrun) reason="provider_usage_overrun";
            else if(InFlight>=MaxConcurrent) reason="concurrency_limit";
            else if(Requests>=MaxRequests) reason="request_budget";
            else if(!Fits(ChargedInputTokens,ReservedInputTokens,input,MaxInputTokens)) reason="input_token_budget";
            else if(!Fits(ChargedOutputTokens,ReservedOutputTokens,output,MaxOutputTokens)) reason="output_token_budget";
            else if(input>long.MaxValue-EstimatedInputTokens)reason="input_estimate_overflow";
            if(reason.Length>0) return false;
            reservation=new Reservation(this,input,output);InFlight++;Requests++;EstimatedInputTokens+=input;ReservedInputTokens+=input;ReservedOutputTokens+=output;return true;
        }
        public void Settle(Reservation reservation,long? input,long? output)
        {
            if(reservation==null||reservation.Owner!=this||reservation.Closed) throw new InvalidOperationException("Reservation missing, foreign, or already settled");
            // Invalid provider accounting must close the circuit without leaking its reservation or manufacturing negative spend.
            if(input<0||output<0) {input=null;output=null;Overrun=true;}
            reservation.Closed=true;InFlight--;ReservedInputTokens-=reservation.Input;ReservedOutputTokens-=reservation.Output;
            if(input.HasValue) MeasuredInputTokens=Add(MeasuredInputTokens,input.Value);if(output.HasValue) MeasuredOutputTokens=Add(MeasuredOutputTokens,output.Value);
            ChargedInputTokens=Add(ChargedInputTokens,input??reservation.Input);ChargedOutputTokens=Add(ChargedOutputTokens,output??reservation.Output);
            if(input>reservation.Input||output>reservation.Output||ChargedInputTokens>MaxInputTokens||ChargedOutputTokens>MaxOutputTokens) Overrun=true;
        }
        static bool Fits(long charged,long reserved,long candidate,long limit)
        {return charged>=0&&reserved>=0&&charged<=limit&&reserved<=limit-charged&&candidate<=limit-charged-reserved;}
        long Add(long total,long value)
        {if(total<0||value<0||value>long.MaxValue-total){Overrun=true;return long.MaxValue;}return total+value;}
    }
}
