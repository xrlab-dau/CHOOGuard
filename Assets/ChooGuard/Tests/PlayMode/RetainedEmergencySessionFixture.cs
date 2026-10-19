#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Tests.PlayMode
{
    // The default remains the current provider. Only explicitly inheriting retained
    // EmergencySession fixtures select its legacy alternative for their own scene loads.
    public abstract class RetainedEmergencySessionFixture
    {
        internal const string ProviderRootName="CHOOGuard Provider Runtime";
        internal const string ProviderTypeName="ChooGuard.Seeded.Unity.PlatformSeededSimulator, ChooGuard.Seeded";
        ProviderAwakeDeferral deferral;
        Type providerType;
        bool subscribed;

        [SetUp]
        public void SelectRetainedEmergencySession()
        {
            try
            {
                providerType=Type.GetType(ProviderTypeName,true);
                deferral=new ProviderAwakeDeferral(providerType);
                SceneManager.sceneLoaded+=SelectRetainedSessionAfterStationLoad;subscribed=true;
            }
            catch{ReleaseRetainedEmergencySession();throw;}
        }

        void SelectRetainedSessionAfterStationLoad(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!=SceneFlow.StationScene)return;
            var providers=scene.GetRootGameObjects().Where(root=>root.GetComponent(providerType)!=null).ToArray();
            Assert.That(providers.Length,Is.EqualTo(1),"The retained session must select exactly the installed provider control root.");
            var provider=providers[0];
            Assert.That(provider.name,Is.EqualTo(ProviderRootName));
            Assert.That(provider.transform.parent,Is.Null);
            Assert.That(provider.GetComponentsInChildren<Renderer>(true),Is.Empty,"A provider selector must never deactivate source render geometry.");
            Assert.That(provider.GetComponentsInChildren<Collider>(true),Is.Empty,"A provider selector must never deactivate source collision geometry.");
            Assert.That(provider.GetComponentsInChildren<MeshFilter>(true),Is.Empty,"A provider selector must never deactivate source mesh geometry.");
            Assert.That(provider.GetComponentsInChildren<Terrain>(true),Is.Empty,"A provider selector must never deactivate terrain geometry.");
            provider.SetActive(false);
        }

        [TearDown]
        public void ReleaseRetainedEmergencySession()
        {
            try{if(subscribed)SceneManager.sceneLoaded-=SelectRetainedSessionAfterStationLoad;}
            finally{subscribed=false;try{deferral?.Dispose();}finally{deferral=null;providerType=null;}}
        }
    }

    // Reflection avoids changing PlayModeTests assembly dependencies. This narrow guard
    // is also used by the separate current-provider test only while its Scene is loading.
    internal sealed class ProviderAwakeDeferral : IDisposable
    {
        readonly PropertyInfo property;
        readonly bool previous;
        bool disposed;
        internal ProviderAwakeDeferral(Type providerType)
        {
            property=providerType.GetProperty("DeferPreparationForLegacyTests",BindingFlags.NonPublic|BindingFlags.Static);
            if(property==null||property.PropertyType!=typeof(bool)||!property.CanRead||!property.CanWrite)
                throw new InvalidOperationException("The scoped provider Awake deferral contract is missing.");
            previous=(bool)property.GetValue(null);
            if(previous)throw new InvalidOperationException("A prior fixture leaked provider Awake deferral.");
            try{property.SetValue(null,true);}
            catch{property.SetValue(null,previous);throw;}
        }
        public void Dispose()
        {if(disposed)return;property.SetValue(null,previous);disposed=true;}
    }
}
#endif
