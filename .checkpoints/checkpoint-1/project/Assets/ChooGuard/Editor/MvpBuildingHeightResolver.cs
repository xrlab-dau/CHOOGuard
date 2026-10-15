using System;
using System.Globalization;
namespace ChooGuard.Editor
{
 public static class MvpBuildingHeightResolver
 {
  [Serializable] public sealed class Resolution
  {
   public string id,sourceHeight,sourceLevels,provenance,geometryStatus;
   public float metres,terrainDatum;
  }
  public static Resolution Resolve(string id,string height,string levels)
  {
   var result=new Resolution{id=id,sourceHeight=height??"",sourceLevels=levels??"",geometryStatus="not_generated"};
   float value;
   if(PositiveNumber((height??"").Trim().Replace(" m",""),out value))
   {result.metres=value;result.provenance="OSM_EXPLICIT_HEIGHT_TAG_NOT_SURVEYED";}
   else if(PositiveNumber(levels,out value))
   {result.metres=value*3f;result.provenance="INFERRED_LEVELS_TIMES_3_METRES";}
   else
   {result.metres=9f;result.provenance="TEMPORARY_UNKNOWN_9M_PROXY_AWAITING_ML_HEIGHT_PROBE_NOT_MEASURED";}
   return result;
  }
  static bool PositiveNumber(string raw,out float value)
  {return float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&value>0&&!float.IsInfinity(value)&&!float.IsNaN(value);}
 }
}
