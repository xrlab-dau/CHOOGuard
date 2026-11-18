using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class CardiacTitleEntryTests
    {
        string previousKey;
        IEnumerator Until(Func<bool> ready,float seconds,string detail)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!ready()&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(ready(),Is.True,detail);
        }
        [UnitySetUp]public IEnumerator Setup()
        {
            previousKey=Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            Environment.SetEnvironmentVariable("TYPESAFE_API_KEY","off");Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync("Bootstrap");yield return null;
        }
        [UnityTest,Timeout(660000)]public IEnumerator LatestCardiacCategoryStartsOnePatientAndReturnsToTheSharedTutorialMenu()
        {
            var title=UnityEngine.Object.FindFirstObjectByType<TitleScreen>();
            yield return Until(()=>title!=null&&title.Built,15,"Actual title ready");
            title.StartButton.onClick.Invoke();title.Modes.TutorialButton.onClick.Invoke();
            var menu=UnityEngine.Object.FindFirstObjectByType<TutorialMenu>();
            menu.GetComponentsInChildren<Button>().Single(b=>b.name=="심정지 환자 대응").onClick.Invoke();
            yield return Until(()=>EmergencySession.Current!=null&&EmergencySession.Current.Incidents!=null&&EmergencySession.Current.Incidents.Practicing,
                360,"Cardiac category enters the retained practice session");
            var session=EmergencySession.Current;
            Assert.That(session.Jev.Available,Is.False,"Practice does not require a JEV key");
            session.Player.SetExternalInputMode(true);Assert.That(session.Player.Resume(false),Is.True);
            yield return Until(()=>HazardRegistry.Active.OfType<CollapseHazard>().Any(),130,"Normal practice produces its patient after settling");
            var patient=HazardRegistry.Active.OfType<CollapseHazard>().Single();
            Assert.That(patient.Level,Is.EqualTo(4));Assert.That(patient.Position.y,Is.InRange(6.8f,8.7f));
            Assert.That(session.Incidents.Practicing,Is.True);Assert.That(session.Jev.Available,Is.False);
            var folder=Environment.GetEnvironmentVariable("CG_FIRE_REVIEW");
            if(!string.IsNullOrEmpty(folder))File.WriteAllText(Path.Combine(folder,"cardiac-title-entry.json"),new JObject{
                ["passedStartup"]=true,["collapseCount"]=1,["level"]=patient.Level,["modelY"]=patient.Position.y,
                ["shiftSeconds"]=session.ShiftSeconds,["jevAvailable"]=session.Jev.Available,
                ["scope"]="Actual title entry, practice startup, one patient, and shared-menu return; not a full CPR playthrough"
            }.ToString());
            SceneFlow.ToTutorialMenu();yield return null;
            yield return Until(()=>UnityEngine.Object.FindFirstObjectByType<TitleScreen>()?.Built==true,30,"Return to actual tutorial menu");
            Assert.That(PracticeShift.Armed,Is.False);
            menu=UnityEngine.Object.FindFirstObjectByType<TutorialMenu>();
            var names=menu.GetComponentsInChildren<Button>().Select(b=>b.name).ToArray();
            Assert.That(names,Does.Contain("역무원 화재대응"));Assert.That(names,Does.Contain("심정지 환자 대응"));
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Bootstrap");yield return null;
            Environment.SetEnvironmentVariable("TYPESAFE_API_KEY",previousKey);
        }
    }
}
