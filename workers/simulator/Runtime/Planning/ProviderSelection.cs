using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    /// <summary>Immutable routing chosen in the browser. Contains no OAuth credentials.</summary>
    public sealed class ProviderSelection
    {
        public const string Version="provider-selection-v1";
        public string ProviderId {get;}
        public string AccountId {get;}
        public string ModelId {get;}
        public string SessionGeneration {get;}
        public string CatalogVersion {get;}
        public string SelectionVersion {get;}
        public string Billing {get;}
        public string Registration {get;}
        public string Fingerprint {get;}
        // A logical binding identifies who selected which model/catalog. An auth lease is separate:
        // refreshing or revoking a lease must never authorize an old network request.
        public string LogicalFingerprint {get;}
        public string CacheVersion {get {return "user-subscription-v3:"+ProviderId+":"+LogicalFingerprint;}}
        ProviderSelection(JObject value)
        {
            ProviderId=(string)value["provider_id"];AccountId=(string)value["account_id"];ModelId=(string)value["model_id"];
            SessionGeneration=(string)value["session_generation"];CatalogVersion=(string)value["catalog_version"];
            SelectionVersion=(string)value["selection_version"];Billing=(string)value["billing"];Registration=(string)value["registration"];
            Fingerprint=Digest(ToToken().ToString(Formatting.None));
            LogicalFingerprint=Digest(ToBindingRecord().ToString(Formatting.None));
        }
        public static ProviderSelection Parse(JToken token)
        {
            var value=token as JObject;
            string[] fields={"provider_id","account_id","model_id","session_generation","catalog_version","selection_version","billing","registration"};
            if(value==null||value.Properties().Count()!=fields.Length||fields.Any(k=>value[k]?.Type!=JTokenType.String))throw new ArgumentException("invalid_provider_selection");
            foreach(string field in new[]{"provider_id","account_id","session_generation"})if(!SubscriptionConnection.Identifier((string)value[field]))throw new ArgumentException("invalid_provider_selection");
            if(!ModelIdentifier((string)value["model_id"])||(string)value["selection_version"]!=Version||(string)value["billing"]!="user_subscription"||(string)value["registration"]!="own_app_public_oauth")throw new ArgumentException("unsupported_provider_selection");
            string hash=(string)value["catalog_version"];
            if(!IsDigest(hash))throw new ArgumentException("invalid_provider_catalog_version");
            return new ProviderSelection(value);
        }
        public static bool ModelIdentifier(string value)
        {return !string.IsNullOrEmpty(value)&&value.Length<=256&&!value.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c))&&!value.Any(char.IsSurrogate);}
        public JObject ToToken()
        {
            return new JObject{["provider_id"]=ProviderId,["account_id"]=AccountId,["model_id"]=ModelId,["session_generation"]=SessionGeneration,
                ["catalog_version"]=CatalogVersion,["selection_version"]=SelectionVersion,["billing"]=Billing,["registration"]=Registration};
        }
        public JObject ToRecord()
        {
            // Replay needs routing provenance, not a private local account handle or display name/email.
            return new JObject{["provider_id"]=ProviderId,["account_ref"]=Digest(ProviderId+"\n"+AccountId),["model_id"]=ModelId,
                ["session_generation"]=SessionGeneration,["catalog_version"]=CatalogVersion,["selection_version"]=SelectionVersion,
                ["billing"]=Billing,["registration"]=Registration,["selection_fingerprint"]=Fingerprint};
        }
        public bool SameAs(ProviderSelection other){return other!=null&&Fingerprint==other.Fingerprint;}
        public bool SameLogicalBinding(ProviderSelection other){return other!=null&&LogicalFingerprint==other.LogicalFingerprint;}
        public JObject ToBindingRecord()
        {
            return new JObject{["schema"]="provider-logical-binding-v1",["provider_id"]=ProviderId,
                ["account_ref"]=Digest(ProviderId+"\n"+AccountId),["model_id"]=ModelId,["catalog_version"]=CatalogVersion,
                ["selection_version"]=SelectionVersion,["billing"]=Billing,["registration"]=Registration};
        }
        public static JObject ValidateBindingRecord(JToken token)
        {
            var record=token as JObject;string[] fields={"schema","provider_id","account_ref","model_id","catalog_version","selection_version","billing","registration"};
            if(record==null||record.Properties().Count()!=fields.Length||fields.Any(k=>record[k]?.Type!=JTokenType.String)||
                (string)record["schema"]!="provider-logical-binding-v1"||!SubscriptionConnection.Identifier((string)record["provider_id"])||
                !IsDigest((string)record["account_ref"])||!IsDigest((string)record["catalog_version"])||!ModelIdentifier((string)record["model_id"])||
                (string)record["selection_version"]!=Version||(string)record["billing"]!="user_subscription"||(string)record["registration"]!="own_app_public_oauth")
                throw new ArgumentException("invalid_logical_provider_binding");
            return new JObject(fields.Select(k=>new JProperty(k,record[k].DeepClone())));
        }
        public static JObject ValidateRecord(JToken token)
        {
            var record=token as JObject;string[] fields={"provider_id","account_ref","model_id","session_generation","catalog_version","selection_version","billing","registration","selection_fingerprint"};
            if(record==null||record.Properties().Count()!=fields.Length||fields.Any(k=>record[k]?.Type!=JTokenType.String)||
                !SubscriptionConnection.Identifier((string)record["provider_id"])||!SubscriptionConnection.Identifier((string)record["session_generation"])||!ModelIdentifier((string)record["model_id"])||
                !IsDigest((string)record["account_ref"])||!IsDigest((string)record["catalog_version"])||!IsDigest((string)record["selection_fingerprint"])||
                (string)record["selection_version"]!=Version||(string)record["billing"]!="user_subscription"||(string)record["registration"]!="own_app_public_oauth")throw new ArgumentException("invalid_recorded_provider_selection");
            return (JObject)record.DeepClone();
        }
        static bool IsDigest(string hash){return hash!=null&&hash.Length==64&&!hash.Any(c=>!((c>='0'&&c<='9')||(c>='a'&&c<='f')));}
        static string Digest(string text)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    }
}
