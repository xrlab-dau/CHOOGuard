using System;
using System.Linq;
using ChooGuard.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    /// <summary>
    /// The crowd's walk and run clips must keep their stride out of the pose. The NavMeshAgent moves people; a stride baked
    /// into the pose carried each body ahead of its agent for the whole cycle and snapped it back about 1.2 m at every loop,
    /// which players saw as walking NPCs rolling back (measured 2026-09-27: 212 snaps in 20 s of crowd).
    /// </summary>
    public sealed class CrowdLocomotionClipTests
    {
        [TestCase("m_walk_neutral_01")]
        [TestCase("f_walk_neutral_01")]
        [TestCase("m_run_neutral_01")]
        [TestCase("f_run_neutral_01")]
        public void StrideStaysOutOfThePose(string name)
        {
            var path = RocketboxImportRules.AnimationRoot + name + ".max.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__", StringComparison.Ordinal));
            Assert.IsNotNull(clip, path);
            Assert.IsFalse(AnimationUtility.GetAnimationClipSettings(clip).loopBlendPositionXZ, name + ": 수평 전진이 포즈에 구워져 있다(주기마다 몸이 뒤로 튄다)");
            Assert.Greater(clip.averageSpeed.magnitude, .5f, name + ": 전진은 뿌리 이동으로 남아 있어야 한다(걸음새 맞춤이 이 속도를 쓴다)");
        }
    }
}
