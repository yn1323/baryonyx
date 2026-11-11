using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    // VFXの質感デバッグルーム。つまみ・形・色・出発点は見本のマテリアルにすぐ届き、次のPlayのために残る。
    public sealed class VfxLookLabSceneTests
    {
        private const string ScenePath = SceneTests.ScenesFolder + "/Debug/VfxLookLab.unity";

        private static readonly string[] IntKeys =
        {
            VfxLookLab.FormKey,
            VfxLookLab.TintKey,
            VfxLookLab.PausedKey,
            VfxLookLab.ActorKey,
        };

        // 開発者が最後に詰めた見た目を、テストで上書きしたまま残さない。
        private string savedLook;
        private readonly Dictionary<string, int> savedInts = new();
        private string clipboard;

        [SetUp]
        public void ForgetTheLook()
        {
            clipboard = GUIUtility.systemCopyBuffer;
            savedLook = PlayerPrefs.HasKey(VfxLookLab.LookKey)
                ? PlayerPrefs.GetString(VfxLookLab.LookKey)
                : null;
            savedInts.Clear();
            foreach (var key in IntKeys)
                if (PlayerPrefs.HasKey(key))
                    savedInts[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(VfxLookLab.LookKey);
            foreach (var key in IntKeys)
                PlayerPrefs.DeleteKey(key);
        }

        [UnityTearDown]
        public IEnumerator RestoreTheLook()
        {
            yield return SceneTests.UnloadAll(nameof(VfxLookLabSceneTests));
            GUIUtility.systemCopyBuffer = clipboard;
            PlayerPrefs.DeleteKey(VfxLookLab.LookKey);
            if (savedLook != null)
                PlayerPrefs.SetString(VfxLookLab.LookKey, savedLook);
            foreach (var key in IntKeys)
            {
                PlayerPrefs.DeleteKey(key);
                if (savedInts.TryGetValue(key, out int value))
                    PlayerPrefs.SetInt(key, value);
            }
        }

        [UnityTest]
        public IEnumerator TheControlsReachTheSampleAndTheLookIsRemembered()
        {
            VfxLookLab lab = null;
            // ホームから開くときのシャッターが開ききってから押す。
            yield return SceneTests.Load<VfxLookLab>(
                ScenePath,
                found =>
                    found.Material != null
                    && (found.Transition == null || !found.Transition.IsPlaying),
                found => lab = found
            );

            // 何も覚えていなければ、今の基準の見た目で、線の見本が再生される。
            Assert.That(lab.Values, Is.EqualTo(VfxLook.Presets[VfxLook.DefaultPreset].Values));
            Assert.That(lab.Form, Is.EqualTo(VfxLookForm.Line));
            Assert.That(lab.Paused, Is.False);
            Assert.That(lab.Board.material, Is.EqualTo(lab.Material));
            Assert.That(
                lab.Material,
                Is.Not.EqualTo(lab.LookMaterial),
                "The asset is not changed."
            );
            float before = lab.Progress;
            yield return SceneTests.WaitUntil(
                () => !Mathf.Approximately(lab.Progress, before),
                message: "The sample plays."
            );
            foreach (
                var button in lab
                    .FormButtons.Concat(lab.TintButtons)
                    .Concat(lab.PresetButtons)
                    .Append(lab.PlayButton)
                    .Append(lab.ActorButton)
                    .Append(lab.CopyButton)
                    .Append(lab.HomeButton)
            )
                SceneTests.AssertTouchSize(button.transform);

            // つまみは見本のマテリアルにすぐ届き、値の表示も変わる。
            int soft = (int)VfxLookParam.EdgeSoft;
            lab.Sliders[soft].value = 0.9f;
            Assert.That(lab.Material.GetFloat("_EdgeSoft"), Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(lab.ValueTexts[soft].text, Is.EqualTo("0.90"));
            // 階調の段は整数で動く。
            lab.Sliders[(int)VfxLookParam.Steps].value = 2.6f;
            Assert.That(lab.Material.GetFloat("_Steps"), Is.EqualTo(3f));
            // 背景の暗さは、見本の後ろの幕に届く。
            lab.Sliders[(int)VfxLookParam.Backdrop].value = 0f;
            Assert.That(lab.Backdrop.enabled, Is.False);
            lab.Sliders[(int)VfxLookParam.Backdrop].value = 0.5f;
            Assert.That(lab.Backdrop.enabled, Is.True);
            Assert.That(lab.Backdrop.color.a, Is.EqualTo(0.5f).Within(0.001f));

            // 床の光と敵への光は、見本が出ている間だけ灯る。1を超えた分は、色より明るく描く。
            lab.Sliders[(int)VfxLookParam.FloorLight].value = 0f;
            lab.Sliders[(int)VfxLookParam.ActorLight].value = 0f;
            yield return null;
            Assert.That(lab.FloorLight.enabled, Is.False);
            Assert.That(lab.ActorLight, Is.Not.Null);
            Assert.That(lab.ActorLight.transform.parent, Is.EqualTo(lab.Actor.transform));
            Assert.That(lab.ActorLight.Lit, Is.False);
            lab.Sliders[(int)VfxLookParam.FloorLight].value = 1f;
            lab.Sliders[(int)VfxLookParam.ActorLight].value = 2f;
            yield return SceneTests.WaitUntil(
                () =>
                    lab.FloorLight.enabled
                    && lab.FloorLight.color.a > 0.5f
                    && lab.ActorLight.Lit
                    && lab.ActorLight.material.GetFloat("_Intensity") > 1.5f,
                message: "The light comes up with the sample."
            );
            Assert.That(lab.BrightnessOf(1f), Is.EqualTo(1f));
            Assert.That(lab.BrightnessOf(2f), Is.EqualTo(1f + lab.LightBoost));

            // 形と色。選んだものに印が付く。
            lab.FormButtons[(int)VfxLookForm.Ring].onClick.Invoke();
            Assert.That(lab.Material.GetFloat("_Form"), Is.EqualTo((float)VfxLookForm.Ring));
            Assert.That(
                lab.FormSelected.Select(mark => mark.activeSelf),
                Is.EqualTo(new[] { false, false, true, false })
            );
            lab.TintButtons[1].onClick.Invoke();
            Assert.That(lab.Board.color, Is.EqualTo(VfxLook.Tints[1].Color));
            Assert.That(lab.TintSelected[1].activeSelf, Is.True);
            lab.ActorButton.onClick.Invoke();
            Assert.That(lab.Actor.activeSelf, Is.False);
            Assert.That(lab.ActorLabel.text, Is.EqualTo(VfxLookLab.ActorHiddenText));

            // 進み具合を動かすと止まり、その瞬間のまま進まない。
            lab.ProgressSlider.value = 0.8f;
            Assert.That(lab.Paused, Is.True);
            Assert.That(lab.PlayLabel.text, Is.EqualTo(VfxLookLab.PausedText));
            yield return null;
            yield return null;
            Assert.That(lab.Progress, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(lab.Board.Shape.y, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(lab.Board.Shape.z, Is.EqualTo(lab.EatenAt(0.8f)).Within(0.001f));
            Assert.That(lab.Board.Shape.z, Is.GreaterThan(0f), "Past the hold, it is going.");

            // 値は文字でコピーでき、次のPlayのために残る。
            lab.CopyButton.onClick.Invoke();
            Assert.That(
                GUIUtility.systemCopyBuffer,
                Is.EqualTo(VfxLook.Describe(VfxLookForm.Ring, 1, lab.Values.ToArray()))
            );
            Assert.That(lab.CopyLabel.text, Is.EqualTo(VfxLookLab.CopiedText));
            var remembered = VfxLook.Parse(PlayerPrefs.GetString(VfxLookLab.LookKey));
            Assert.That(remembered, Is.EqualTo(lab.Values.ToArray()).Within(0.0005f));
            Assert.That(PlayerPrefs.GetInt(VfxLookLab.FormKey), Is.EqualTo((int)VfxLookForm.Ring));
            Assert.That(PlayerPrefs.GetInt(VfxLookLab.TintKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(VfxLookLab.PausedKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(VfxLookLab.ActorKey), Is.Zero);

            // 出発点を押すと、つまみがその見た目へ戻る。
            lab.PresetButtons[0].onClick.Invoke();
            Assert.That(lab.Values, Is.EqualTo(VfxLook.Presets[0].Values));
            Assert.That(
                lab.Sliders[soft].value,
                Is.EqualTo(VfxLook.Presets[0].Values[soft]).Within(0.001f)
            );
            Assert.That(
                lab.Material.GetFloat("_EdgeSoft"),
                Is.EqualTo(VfxLook.Presets[0].Values[soft]).Within(0.001f)
            );

            // 再生に戻すと、止めた所から進む。
            lab.PlayButton.onClick.Invoke();
            Assert.That(lab.Paused, Is.False);
            Assert.That(lab.PlayLabel.text, Is.EqualTo(VfxLookLab.PlayingText));
        }
    }
}
