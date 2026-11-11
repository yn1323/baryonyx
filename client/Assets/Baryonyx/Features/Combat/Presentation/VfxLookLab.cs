using System.Collections;
using System.Collections.Generic;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The look lab, a debugging room for the look the effects are built on
    /// (doc/features/screens/debug-rooms.md): one sample (a stroke, an orb, a ring or a mass of
    /// smoke) drawn on the battle's stage with the battle's camera and bloom, over an enemy, and
    /// a slider for every part of its look (<see cref="VfxLook"/>): how hard or misty its edge
    /// is, its core, halo and haze, the noise, how it is blended and how it goes. The sample
    /// plays again and again, or stands still at a moment picked with the bar under it. The look
    /// is remembered between Plays and can be copied as text.
    /// </summary>
    public sealed class VfxLookLab : MonoBehaviour
    {
        // What is remembered between Plays (on this machine only).
        public const string LookKey = "Baryonyx.VfxLookLab.Look";
        public const string FormKey = "Baryonyx.VfxLookLab.Form";
        public const string TintKey = "Baryonyx.VfxLookLab.Tint";
        public const string PausedKey = "Baryonyx.VfxLookLab.Paused";
        public const string ActorKey = "Baryonyx.VfxLookLab.Actor";

        public const string PlayingText = "止める";
        public const string PausedText = "再生";
        public const string ActorShownText = "相手：あり";
        public const string ActorHiddenText = "相手：なし";
        public const string CopyText = "値をコピー";
        public const string CopiedText = "コピーしました";

        [Header("見本")]
        [Tooltip("見本を描く板。")]
        public BattleVfxImage Board;

        [Tooltip(
            "見本のマテリアル（Vfx Look Lab シェーダー）。Play中は複製を動かし、アセットは変えない。"
        )]
        public Material LookMaterial;

        [Tooltip("見本の後ろで舞台を暗くする幕。")]
        public Graphic Backdrop;

        [Tooltip("見本を重ねる相手（敵）。")]
        public GameObject Actor;

        [Tooltip("見本が床に落とす光だまり。相手の足元に、相手より奥へ描く。")]
        public BattleVfxImage FloorLight;

        [Tooltip("床を這う余波を描く板。見本と同じマテリアルの複製で、余波だけを描く。")]
        public BattleVfxImage Wake;

        [Tooltip("線の見本のとき、余波の板を傾ける角度（度）。切っ先から右下へ走る。")]
        public float WakeTilt = -9f;

        [Tooltip(
            "相手を照らす光のマテリアル（戦闘と同じ、絵の形の加算の光）。Play中は複製を動かす。"
        )]
        public Material ActorLightMaterial;

        [Tooltip("光が1を超えたとき、超えた分1につき明るさを何倍ぶん足すか。")]
        [Min(0f)]
        public float LightBoost = 2f;

        [Tooltip("見本が現れた直後の光の強まり（1で2倍まで）。")]
        [Min(0f)]
        public float LightFlash = 0.6f;

        [Tooltip("線の見本を傾ける角度（度）。")]
        public float LineTilt = -24f;

        [Tooltip(
            "床の高さ。見本の板の中心から上へ測り、板の半分の高さを1とする（下は負）。もやが床に溜まる高さになる。"
        )]
        public float FloorHeight = -0.235f;

        [Header("時間")]
        [Tooltip("見本が出てから消えるまでの長さ（秒。速さ1のとき）。")]
        [Min(0.1f)]
        public float LifeSeconds = 1.4f;

        [Tooltip("消えてから次に出るまでの間（秒）。")]
        [Min(0f)]
        public float GapSeconds = 0.4f;

        [Header("操作")]
        [Tooltip("つまみ（VfxLookParam の順）。")]
        public Slider[] Sliders = new Slider[0];

        [Tooltip("つまみの今の値（Sliders と同じ順）。")]
        public TMP_Text[] ValueTexts = new TMP_Text[0];

        [Tooltip("形のボタン（VfxLookForm の順）と、選んだ印。")]
        public Button[] FormButtons = new Button[0];
        public GameObject[] FormSelected = new GameObject[0];

        [Tooltip("色のボタン（VfxLook.Tints の順）と、選んだ印。")]
        public Button[] TintButtons = new Button[0];
        public GameObject[] TintSelected = new GameObject[0];

        [Tooltip("見た目の出発点のボタン（VfxLook.Presets の順）。")]
        public Button[] PresetButtons = new Button[0];

        public Button PlayButton;
        public TMP_Text PlayLabel;

        [Tooltip("進み具合。再生中は見本に合わせて動き、動かすと止まってその瞬間を見せる。")]
        public Slider ProgressSlider;

        public Button ActorButton;
        public TMP_Text ActorLabel;
        public Button CopyButton;
        public TMP_Text CopyLabel;

        [Header("ホームへ戻る")]
        [Tooltip("ホームへ戻るボタン。戻り先のシーンがないとき（展示室のPrefabなど）は隠す。")]
        public Button HomeButton;

        [Tooltip("戻るときにシーンを覆う遷移の演出。")]
        public SceneTransitionController Transition;

        [Tooltip("戻り先のシーンの名前。空なら戻るボタンを隠す。")]
        public string HomeSceneName = "";

        // The same pattern every time, so two looks are compared on the same noise.
        private const float Seed = 0.37f;

        private float[] values = VfxLook.Preset(VfxLook.DefaultPreset);
        private Material material;
        private Material actorLightMaterial;
        private Material wakeMaterial;
        private Vector2 sampleAt;
        private BattleActorLight actorLight;
        private float clock;
        private float noiseTime;
        private Coroutine copied;

        /// <summary>The look's values, in <see cref="VfxLookParam"/>'s order.</summary>
        public IReadOnlyList<float> Values => values;

        public VfxLookForm Form { get; private set; }

        /// <summary>The tint picked (an index into <see cref="VfxLook.Tints"/>).</summary>
        public int Tint { get; private set; }

        /// <summary>Whether the sample stands still at the moment the bar shows.</summary>
        public bool Paused { get; private set; }

        public bool ActorShown { get; private set; } = true;

        /// <summary>How far along the sample is (0 to 1).</summary>
        public float Progress { get; private set; }

        /// <summary>The material the sample is drawn with while playing (a copy of the asset).</summary>
        public Material Material => material;

        /// <summary>Whether the home button was pressed and the lab is leaving for home.</summary>
        public bool Leaving { get; private set; }

        private bool HasHome => !string.IsNullOrWhiteSpace(HomeSceneName);

        private void Start()
        {
            Listen(HomeButton, GoHome);
            if (HomeButton != null)
                HomeButton.gameObject.SetActive(HasHome);
            if (Board == null || LookMaterial == null)
                return;
            material = new Material(LookMaterial)
            {
                name = LookMaterial.name + " (Lab)",
                hideFlags = HideFlags.DontSave,
            };
            Board.material = material;
            sampleAt = Board.rectTransform.anchoredPosition;
            if (Wake != null)
            {
                wakeMaterial = new Material(LookMaterial)
                {
                    name = LookMaterial.name + " (Lab wake)",
                    hideFlags = HideFlags.DontSave,
                };
                Wake.material = wakeMaterial;
            }
            AddActorLight();

            for (int i = 0; i < Sliders.Length && i < VfxLook.Count; i++)
            {
                var slider = Sliders[i];
                if (slider == null)
                    continue;
                var spec = VfxLook.Specs[i];
                slider.minValue = spec.Min;
                slider.maxValue = spec.Max;
                slider.wholeNumbers = spec.Whole;
                var param = (VfxLookParam)i;
                slider.onValueChanged.AddListener(value => Set(param, value));
            }
            for (int i = 0; i < FormButtons.Length; i++)
            {
                var form = (VfxLookForm)i;
                Listen(FormButtons[i], () => SetForm(form));
            }
            for (int i = 0; i < TintButtons.Length; i++)
            {
                int tint = i;
                Listen(TintButtons[i], () => SetTint(tint));
            }
            for (int i = 0; i < PresetButtons.Length; i++)
            {
                int preset = i;
                Listen(PresetButtons[i], () => ApplyPreset(preset));
            }
            Listen(PlayButton, () => SetPaused(!Paused));
            Listen(ActorButton, () => SetActorShown(!ActorShown));
            Listen(CopyButton, Copy);
            if (ProgressSlider != null)
            {
                ProgressSlider.minValue = 0f;
                ProgressSlider.maxValue = 1f;
                ProgressSlider.onValueChanged.AddListener(Scrub);
            }

            Load();
            Refresh();
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
            if (actorLightMaterial != null)
                Destroy(actorLightMaterial);
            if (wakeMaterial != null)
                Destroy(wakeMaterial);
        }

        // The light on the enemy, as the battle's effects light a character: a copy of its
        // picture over it, added in the light's colour (BattleSkillVfx.RegisterActor).
        private void AddActorLight()
        {
            var sprite = Actor != null ? Actor.GetComponent<RawImage>() : null;
            if (sprite == null || ActorLightMaterial == null)
                return;
            actorLightMaterial = new Material(ActorLightMaterial)
            {
                name = ActorLightMaterial.name + " (Lab)",
                hideFlags = HideFlags.DontSave,
            };
            var rect = new GameObject(
                "EffectLight",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            rect.SetParent(sprite.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            actorLight = rect.gameObject.AddComponent<BattleActorLight>();
            actorLight.texture = sprite.texture;
            actorLight.uvRect = sprite.uvRect;
            actorLight.material = actorLightMaterial;
            actorLight.raycastTarget = false;
            actorLight.SetCorners(Color.clear, Color.clear, Color.clear, Color.clear);
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private void Update()
        {
            if (material == null)
                return;
            if (!Paused)
            {
                float step = Time.deltaTime * values[(int)VfxLookParam.Speed];
                clock += step;
                noiseTime += step;
                if (clock > LifeSeconds + GapSeconds)
                    clock = 0f;
                Progress = Mathf.Clamp01(clock / LifeSeconds);
                if (ProgressSlider != null)
                    ProgressSlider.SetValueWithoutNotify(Progress);
            }
            Draw();
        }

        /// <summary>How much of the sample is eaten away at <paramref name="progress"/>.</summary>
        public float EatenAt(float progress) =>
            progress <= Hold ? 0f : Mathf.Clamp01((progress - Hold) / (1f - Hold));

        /// <summary>How far through its life the sample starts to go (the look's value).</summary>
        public float Hold => values[(int)VfxLookParam.Hold];

        private void Draw()
        {
            float eaten = EatenAt(Progress);
            Board.SetShape(Seed, Progress, eaten, noiseTime);
            if (Wake != null)
            {
                Wake.SetShape(Seed, Progress, eaten, noiseTime);
                Wake.color = VfxLook.Tints[Tint].Color;
                Wake.enabled = values[(int)VfxLookParam.SubWake] > 0f;
            }
            // The light on the floor and on the enemy comes up with the sample, flares just
            // after, and goes as the sample is eaten away. Up to 1 a slider is how much of the
            // light shows; above 1 it makes the light brighter than its colour.
            float flare = Progress - 0.12f;
            float shown =
                Mathf.Clamp01(Progress / 0.08f)
                * (1f - eaten)
                * (1f + LightFlash * Mathf.Exp(-flare * flare / 0.01f));
            var tint = VfxLook.Tints[Tint].Color;
            float floor = values[(int)VfxLookParam.FloorLight] * shown;
            if (FloorLight != null)
            {
                FloorLight.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(floor));
                // A stronger light reaches further over the floor, as well as burning brighter:
                // only brighter, the pool would fill out to its rim and read as a flat plate.
                float past = Mathf.Max(0f, values[(int)VfxLookParam.FloorLight] - 1f);
                FloorLight.rectTransform.localScale = Vector3.one * (1f + 0.5f * past);
                FloorLight.SetShape(Seed, Progress, 0f, 1f + (BrightnessOf(floor) - 1f) * 0.5f);
                FloorLight.enabled = floor > 0f;
            }
            float onActor = values[(int)VfxLookParam.ActorLight] * shown;
            if (actorLight != null)
            {
                // The side of the enemy toward the sample's middle is the body; the light is
                // even over it, a little stronger at the top where the sample stands.
                var lit = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(onActor));
                var low = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(onActor) * 0.75f);
                actorLight.SetCorners(low, lit, lit, low);
                actorLightMaterial.SetFloat("_Intensity", BrightnessOf(onActor));
            }
        }

        // The wake along the floor has a board of its own, drawn with the look's values: from
        // the stroke's tip on down to the right, the way the blade went, or flat under a form
        // that stands, spreading both ways along the floor.
        private void PlaceWake(float tilt)
        {
            if (Wake == null || wakeMaterial == null)
                return;
            wakeMaterial.CopyPropertiesFromMaterial(material);
            wakeMaterial.SetFloat("_Form", 4f);
            bool stroke = Form == VfxLookForm.Line;
            wakeMaterial.SetFloat("_WakeDrag", stroke ? 1f : 0f);
            var wake = Wake.rectTransform;
            float half = Board.rectTransform.rect.height * 0.5f;
            if (stroke)
            {
                var tip = (Vector2)(Quaternion.Euler(0f, 0f, tilt) * (VfxLook.LineTip * half));
                // The wake's root lies 0.82 of the way to the board's left end.
                var toMiddle = (Vector2)(
                    Quaternion.Euler(0f, 0f, WakeTilt)
                    * new Vector2(0.82f * wake.rect.width * 0.5f, 0f)
                );
                wake.anchoredPosition = sampleAt + tip + toMiddle;
                wake.localEulerAngles = new Vector3(0f, 0f, WakeTilt);
            }
            else
            {
                wake.anchoredPosition = sampleAt + new Vector2(0f, FloorHeight * half);
                wake.localEulerAngles = Vector3.zero;
            }
        }

        /// <summary>How much brighter than its colour a light of this strength is drawn.</summary>
        public float BrightnessOf(float strength) => 1f + Mathf.Max(0f, strength - 1f) * LightBoost;

        /// <summary>The light on the enemy while playing, or null before it is made.</summary>
        public BattleActorLight ActorLight => actorLight;

        /// <summary>Sets one value of the look (kept inside its slider's range) and remembers it.</summary>
        public void Set(VfxLookParam param, float value)
        {
            values[(int)param] = VfxLook.Clamp(param, value);
            Save();
            Refresh();
        }

        /// <summary>Takes one of the looks to start from (<see cref="VfxLook.Presets"/>).</summary>
        public void ApplyPreset(int preset)
        {
            values = VfxLook.Preset(preset);
            Save();
            Refresh();
        }

        public void SetForm(VfxLookForm form)
        {
            Form = form;
            Save();
            Refresh();
        }

        public void SetTint(int tint)
        {
            Tint = Mathf.Clamp(tint, 0, VfxLook.Tints.Length - 1);
            Save();
            Refresh();
        }

        /// <summary>Stops the sample at the moment it is at, or lets it play on from there.</summary>
        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (!paused)
                clock = Progress * LifeSeconds;
            Save();
            Refresh();
        }

        public void SetActorShown(bool shown)
        {
            ActorShown = shown;
            Save();
            Refresh();
        }

        // The bar moved by hand: the sample stops at that moment.
        private void Scrub(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            if (!Paused)
                SetPaused(true);
        }

        /// <summary>Copies the look as text (<see cref="VfxLook.Describe"/>) to the clipboard.</summary>
        public void Copy()
        {
            GUIUtility.systemCopyBuffer = VfxLook.Describe(Form, Tint, values);
            if (CopyLabel == null)
                return;
            if (copied != null)
                StopCoroutine(copied);
            copied = StartCoroutine(ShowCopied());
        }

        private IEnumerator ShowCopied()
        {
            CopyLabel.text = CopiedText;
            yield return new WaitForSecondsRealtime(1.2f);
            CopyLabel.text = CopyText;
            copied = null;
        }

        /// <summary>Goes back to home behind the shutter. If home cannot be loaded, the lab stays.</summary>
        public void GoHome()
        {
            if (Leaving)
                return;
            Leaving = SceneLoader.Load(HomeSceneName, Transition, this, () => Leaving = false);
        }

        // Shows the look on the sample and on the controls.
        private void Refresh()
        {
            if (material == null)
                return;
            for (int i = 0; i < VfxLook.Count; i++)
            {
                var spec = VfxLook.Specs[i];
                if (spec.Property != null)
                    material.SetFloat(spec.Property, values[i]);
                if (i < Sliders.Length && Sliders[i] != null)
                    Sliders[i].SetValueWithoutNotify(values[i]);
                if (i < ValueTexts.Length && ValueTexts[i] != null)
                    ValueTexts[i].text = VfxLook.Format(spec, values[i]);
            }
            material.SetFloat("_Form", (int)Form);
            Board.color = VfxLook.Tints[Tint].Color;
            float tilt = Form == VfxLookForm.Line ? LineTilt : 0f;
            Board.rectTransform.localEulerAngles = new Vector3(0f, 0f, tilt);
            // The board may lie tilted, but up stays up: the haze rises, sinks and lies on the
            // floor by the screen's up, given here in the board's own axes.
            float turned = tilt * Mathf.Deg2Rad;
            material.SetVector("_Up", new Vector4(Mathf.Sin(turned), Mathf.Cos(turned), 0f, 0f));
            material.SetFloat("_FloorAt", FloorHeight);
            // The form stops giving off haze as it starts to go.
            material.SetFloat("_EmitStop", Hold);
            PlaceWake(tilt);

            if (Backdrop != null)
            {
                float dark = values[(int)VfxLookParam.Backdrop];
                var color = Backdrop.color;
                Backdrop.color = new Color(color.r, color.g, color.b, dark);
                Backdrop.enabled = dark > 0f;
            }
            if (Actor != null)
                Actor.SetActive(ActorShown);

            for (int i = 0; i < FormSelected.Length; i++)
                if (FormSelected[i] != null)
                    FormSelected[i].SetActive(i == (int)Form);
            for (int i = 0; i < TintSelected.Length; i++)
                if (TintSelected[i] != null)
                    TintSelected[i].SetActive(i == Tint);
            if (PlayLabel != null)
                PlayLabel.text = Paused ? PausedText : PlayingText;
            if (ActorLabel != null)
                ActorLabel.text = ActorShown ? ActorShownText : ActorHiddenText;
            if (ProgressSlider != null)
                ProgressSlider.SetValueWithoutNotify(Progress);
            Draw();
        }

        private void Load()
        {
            values =
                VfxLook.Parse(PlayerPrefs.GetString(LookKey, ""))
                ?? VfxLook.Preset(VfxLook.DefaultPreset);
            Form = (VfxLookForm)
                Mathf.Clamp(PlayerPrefs.GetInt(FormKey, 0), 0, VfxLook.FormNames.Length - 1);
            Tint = Mathf.Clamp(PlayerPrefs.GetInt(TintKey, 0), 0, VfxLook.Tints.Length - 1);
            Paused = PlayerPrefs.GetInt(PausedKey, 0) != 0;
            ActorShown = PlayerPrefs.GetInt(ActorKey, 1) != 0;
            // Stopped, it opens at the moment just before it starts to go.
            if (Paused)
                Progress = Hold;
        }

        private void Save()
        {
            PlayerPrefs.SetString(LookKey, VfxLook.Join(values));
            PlayerPrefs.SetInt(FormKey, (int)Form);
            PlayerPrefs.SetInt(TintKey, Tint);
            PlayerPrefs.SetInt(PausedKey, Paused ? 1 : 0);
            PlayerPrefs.SetInt(ActorKey, ActorShown ? 1 : 0);
        }
    }
}
