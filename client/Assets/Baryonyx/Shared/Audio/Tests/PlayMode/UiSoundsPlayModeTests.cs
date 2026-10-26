using System.Collections;
using System.Collections.Generic;
using Baryonyx.Audio;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    // 押したときの音（doc/rules/sound.md#押したときの音）：起動時に置いた再生の部品が、押せるボタンでは
    // 押した音、押せないボタンでは押せない音を鳴らし、すりガラスのカードは長押しのあとに鳴らさないことを確かめる。
    public sealed class UiSoundsPlayModeTests
    {
        private readonly List<UiSound> played = new();
        private readonly List<GameObject> made = new();

        [SetUp]
        public void Listen()
        {
            played.Clear();
            UiSounds.Played += played.Add;
        }

        [TearDown]
        public void Clean()
        {
            UiSounds.Played -= played.Add;
            foreach (var go in made)
                Object.Destroy(go);
            made.Clear();
        }

        [UnityTest]
        public IEnumerator AButtonPlaysThePressSoundAndOneThatCannotBePressedTheDeniedSound()
        {
            UiSounds.Install();
            Assert.That(UiSounds.Installed, Is.Not.Null, "UiSoundBank.asset is not set up.");
            Assert.That(UiSounds.Installed.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            var button = New("Button").AddComponent<Button>();
            yield return null;
            Assert.That(button.GetComponent<UiClickSound>(), Is.Not.Null);

            Click(button.gameObject);
            button.interactable = false;
            Click(button.gameObject);

            Assert.That(played, Is.EqualTo(new[] { UiSound.Press, UiSound.Denied }));
        }

        [UnityTest]
        public IEnumerator AGlassCardPlaysItsOwnSoundAndNoneWhenTheFingerLiftsAfterAHold()
        {
            UiSounds.Install();
            var go = New("Card");
            go.AddComponent<Button>();
            var card = go.AddComponent<GlassCard>();
            card.HoldSeconds = 0.1f;
            yield return null;
            Assert.That(go.GetComponent<UiClickSound>(), Is.Null);

            Click(go);
            Assert.That(played, Is.EqualTo(new[] { UiSound.Press }));

            ExecuteEvents.Execute(go, Pointer(), ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(0.2f);
            Click(go);
            Assert.That(played, Is.EqualTo(new[] { UiSound.Press }));
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            made.Add(go);
            return go;
        }

        private static PointerEventData Pointer() =>
            new(EventSystem.current) { button = PointerEventData.InputButton.Left };

        private static void Click(GameObject go) =>
            ExecuteEvents.Execute(go, Pointer(), ExecuteEvents.pointerClickHandler);
    }
}
