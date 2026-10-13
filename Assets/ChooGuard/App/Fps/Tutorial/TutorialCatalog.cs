using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Tutorial
{
    [Serializable]
    public sealed class TutorialCategory
    {
        public string Id, Title;
        [TextArea] public string Description;
        public string SceneName;
        public int ExpectedMinSeconds = 150, ExpectedMaxSeconds = 300;
        public string Duration => $"약 {Minutes(ExpectedMinSeconds)}~{Minutes(ExpectedMaxSeconds)}";
        private static string Minutes(int seconds) => seconds % 60 == 0 ? $"{seconds / 60}분" : $"{seconds / 60}분 {seconds % 60}초";
    }

    /// <summary>Author another category and its scene here; the menu and loading flow do not need a new branch.</summary>
    [CreateAssetMenu(menuName = "ChooGuard/Tutorial catalog")]
    public sealed class TutorialCatalog : ScriptableObject
    {
        public const string ResourceName = "TutorialCatalog";
        public TutorialCategory[] Categories = Array.Empty<TutorialCategory>();
        public bool Validate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (Categories == null || Categories.Length == 0) { reason = "등록된 튜토리얼이 없습니다."; return false; }
            foreach (var category in Categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Id) || !ids.Add(category.Id) ||
                    string.IsNullOrWhiteSpace(category.Title) || string.IsNullOrWhiteSpace(category.SceneName) ||
                    category.ExpectedMinSeconds <= 0 || category.ExpectedMaxSeconds < category.ExpectedMinSeconds)
                { reason = "튜토리얼 카테고리 자료를 확인해 주세요."; return false; }
            }
            reason = null; return true;
        }
    }
}
