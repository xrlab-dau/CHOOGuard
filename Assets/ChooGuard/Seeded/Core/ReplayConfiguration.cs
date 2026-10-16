using System;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public static class ReplayConfiguration
    {
        /// <summary>Exact recorded configuration, not a silently selected default.
        /// Current native FPS has a fixed half-second step; rate is a bounded upstream input.</summary>
        public static void Validate(JToken configuration,World saved,bool nativeHalfSecondStep=true)
        {
            if(configuration?.Type!=JTokenType.Object||saved==null)throw new ArgumentException("recorded_configuration_required");
            double step=WorldStateCodec.ReadExactNumber(configuration["step_seconds"]);
            double rate=WorldStateCodec.ReadExactNumber(configuration["rate_multiplier"]);
            if(step<=0||step>60||nativeHalfSecondStep&&step!=.5||rate<0||rate>4)throw new ArgumentException("recorded_configuration_out_of_range");
            if(InitialConditionMaterial.FloatBits(step)!=InitialConditionMaterial.FloatBits(saved.StepSeconds)||InitialConditionMaterial.FloatBits(rate)!=InitialConditionMaterial.FloatBits(saved.RateMultiplier))
                throw new ArgumentException("recorded_configuration_state_mismatch");
        }
    }
}
