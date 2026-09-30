using System;
using ChooGuard.App.Fps.Emergency;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// Title panel for this machine's JEV key: where the key in use comes from, a masked field for the user's own key,
    /// check-and-save (the server is asked with <see cref="JevKey.Check"/>) and removal. The key is written only to
    /// ~/.chooguard/typesafe.key (<see cref="JevKey.Save"/>).
    /// </summary>
    public sealed class JevKeyPanel : MonoBehaviour
    {
        private Action close;
        private TMP_Text source, status;
        private TMP_InputField field;
        private Button remove;
        private bool busy;

        /// <summary>Raised when the stored key or what the server said about it changed.</summary>
        public event Action Changed;

        public static JevKeyPanel Create(RectTransform parent, TMP_FontAsset font, Action onClose)
        {
            // 타이틀의 왼쪽 음영(폭 560) 안에 들어가도록 내용 폭을 470 으로 둔다.
            var root = FpsUiFactory.Node(parent, "JEV 연결");
            FpsUiFactory.Place(root, new Vector2(0, .5f), new Vector2(72, 0), new Vector2(470, 540));
            var panel = root.gameObject.AddComponent<JevKeyPanel>();
            panel.close = onClose;
            var heading = FpsUiFactory.Text(root, font, "제목", new Vector2(0, 1), Vector2.zero, new Vector2(470, 60), 40, TextAlignmentOptions.BottomLeft);
            heading.text = "JEV 연결";
            heading.fontStyle = FontStyles.Bold;

            var about = FpsUiFactory.Text(root, font, "설명", new Vector2(0, 1), new Vector2(0, -78), new Vector2(470, 120), 17, TextAlignmentOptions.TopLeft);
            about.text = "근무 중 비상상황은 JEV 가 지금 역의 상황을 보고 판단해 만듭니다. 이 PC 에서 처음 실행한다면 본인의 TypeSafe JEV API 키를 붙여 넣으세요.\n" +
                "키는 이 PC 사용자 폴더의 .chooguard/typesafe.key 에만 저장되고 저장소·빌드·기록에는 들어가지 않습니다.";
            about.color = FpsUiFactory.TextDim;

            panel.source = FpsUiFactory.Text(root, font, "키 출처", new Vector2(0, 1), new Vector2(0, -214), new Vector2(470, 28), 17, TextAlignmentOptions.TopLeft);
            panel.status = FpsUiFactory.Text(root, font, "상태", new Vector2(0, 1), new Vector2(0, -244), new Vector2(470, 56), 19, TextAlignmentOptions.TopLeft);
            panel.field = FpsUiFactory.InputField(root, font, "키 입력", new Vector2(0, 1), new Vector2(0, -306), new Vector2(470, 52), 20, "JEV API 키 붙여넣기", true);
            panel.field.onSubmit.AddListener(_ => panel.Save());

            FpsUiFactory.Button(root, font, "확인 후 저장", new Vector2(0, 1), new Vector2(0, -374), new Vector2(227, 52), panel.Save, 21);
            panel.remove = FpsUiFactory.Button(root, font, "이 PC 키 지우기", new Vector2(0, 1), new Vector2(243, -374), new Vector2(227, 52), panel.Remove, 21);
            FpsUiFactory.Button(root, font, "돌아가기", new Vector2(0, 1), new Vector2(0, -442), new Vector2(227, 52), () => panel.close?.Invoke(), 21);
            root.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Opens the panel; <paramref name="reason"/> (optional) says why it was opened, e.g. a shift needs a key.</summary>
        public void Show(string reason = null)
        {
            gameObject.SetActive(true);
            Refresh();
            if (!string.IsNullOrEmpty(reason)) Say(reason, FpsUiFactory.Danger);
            field.text = "";
            field.Select();
            field.ActivateInputField();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Refresh()
        {
            var key = JevKey.Load(out var from);
            source.text = from switch
            {
                JevKeySource.Environment => "사용 중인 키: 환경 변수 TYPESAFE_API_KEY (이 창에서 저장한 키보다 먼저 씀)",
                JevKeySource.File => "사용 중인 키: 이 PC 에 저장된 키",
                JevKeySource.Off => "TYPESAFE_API_KEY=off · JEV 가 꺼져 있습니다",
                _ => JevKey.VariableUnusable ? "환경 변수 TYPESAFE_API_KEY 값이 키 형식이 아닙니다" : "이 PC 에 저장된 키가 없습니다",
            };
            source.color = key != null ? Color.white : FpsUiFactory.Danger;
            remove.interactable = JevKey.FileExists;
            if (busy) return;
            if (key == null) Say(from == JevKeySource.Off ? "JEV 가 꺼져 있어 비상상황이 만들어지지 않습니다." : "키를 입력하면 JEV 서버에 확인한 뒤 저장합니다.", FpsUiFactory.TextDim);
            else
            {
                var known = JevKey.KnownFor(key);
                Say(Describe(known), known == JevKeyCheck.Rejected ? FpsUiFactory.Danger : known == JevKeyCheck.Accepted ? FpsUiFactory.Accent : FpsUiFactory.TextDim);
            }
        }

        public static string Describe(JevKeyCheck check)
        {
            switch (check)
            {
                case JevKeyCheck.Accepted: return "JEV 가 키를 확인했습니다.";
                case JevKeyCheck.Rejected: return "JEV 가 이 키를 거부했습니다. 새 키를 입력하세요.";
                case JevKeyCheck.Unreachable: return "JEV 서버에 연결하지 못했습니다. 네트워크를 확인하세요.";
                case JevKeyCheck.Checking: return "JEV 서버에 키를 확인하는 중…";
                default: return "키가 있습니다. 아직 서버에 확인하지 않았습니다.";
            }
        }

        private void Say(string text, Color colour)
        {
            status.text = text;
            status.color = colour;
        }

        private void Save()
        {
            if (busy) return;
            var key = field.text.Trim();
            if (!JevKey.Plausible(key)) { Say("키 형식이 아닙니다. 공백 없이 20자 이상인 키 전체를 붙여 넣으세요.", FpsUiFactory.Danger); return; }
            busy = true;
            Say(Describe(JevKeyCheck.Checking), FpsUiFactory.TextDim);
            StartCoroutine(JevKey.Check(key, result =>
            {
                busy = false;
                if (result == JevKeyCheck.Rejected) { Say(Describe(result), FpsUiFactory.Danger); Changed?.Invoke(); return; }
                try { JevKey.Save(key, result); }
                catch (Exception) { Say("키를 저장하지 못했습니다: " + JevKey.FilePath, FpsUiFactory.Danger); return; }
                field.text = "";
                Refresh();
                Say(result == JevKeyCheck.Accepted ? "확인했습니다. 이 PC 에 저장했습니다." : "서버에 연결하지 못했지만 키를 저장했습니다. 근무 중 연결되면 씁니다.", result == JevKeyCheck.Accepted ? FpsUiFactory.Accent : FpsUiFactory.TextDim);
                Changed?.Invoke();
            }));
        }

        private void Remove()
        {
            if (busy) return;
            bool removed = JevKey.Delete();
            Refresh();
            Say(removed ? "이 PC 에 저장된 키를 지웠습니다." : "지울 키가 없습니다.", FpsUiFactory.TextDim);
            Changed?.Invoke();
        }
    }
}
