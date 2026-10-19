public static class C180ExactBnc003ExitObservation {
 public static string Observe() {
  using (var sha = System.Security.Cryptography.SHA256.Create()) {
   var actual = System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(@"/Users/um-yunsang/CHOOGuard/.state/ACTIVE-2F-SHOPS-CONSTRUCTION-R25/bnc-east/east-correction003/APPLIED-EAST-BRANCH-CORRECTION-003.json"))).Replace("-", "").ToLowerInvariant();
   if (actual != "ef697798362d43623c2ff691c970855ffdf702080aac5770751f9860a1690b34") throw new System.InvalidOperationException("Exact terminal save identity mismatch");
  return "r25-bnc-east-exact-branch-correction-003:2fe6a838228845adb1a368cb77c58d4e:MAIN_THREAD_CALLBACK_ENTERED";
  }
 }
}
