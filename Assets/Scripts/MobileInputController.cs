using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Unity.FPS.Gameplay
{
    public class MobileInputController : MonoBehaviour
    {
        public static MobileInputController Instance { get; private set; }

        [Header("Visibility")]
        public bool ForceShowInEditor;
        public bool HideWhenNotMobile = true;

        [Header("Sensitivity")]
        public float LookSensitivity = 0.09f;
        public float JoystickRadius = 86f;

        public Vector2 MoveInput { get; private set; }
        public bool FireHeld { get; private set; }
        public bool SprintHeld { get; private set; }

        Canvas m_Canvas;
        RectTransform m_JoystickHandle;
        Vector2 m_LookDelta;
        bool m_JumpPressed;
        bool m_ReloadPressed;
        bool m_FirePressed;
        bool m_FireReleased;
        bool m_GameplayControlsAllowed = true;

        public static bool ShouldUseMobileControls
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
#endif
            }
        }

        public static MobileInputController EnsureExists()
        {
            if (Instance != null)
                return Instance;

            GameObject controlsObject = new GameObject("Mobile Input Controller");
            MobileInputController controller = controlsObject.AddComponent<MobileInputController>();
            return controller;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            BuildUi();
            UpdateVisibility();
        }

        void OnEnable()
        {
            UpdateVisibility();
        }

        void Update()
        {
            UpdateVisibility();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public Vector2 ConsumeLookDelta()
        {
            Vector2 value = m_LookDelta;
            m_LookDelta = Vector2.zero;
            return value;
        }

        public bool ConsumeJumpPressed()
        {
            if (!m_JumpPressed)
                return false;

            m_JumpPressed = false;
            return true;
        }

        public bool ConsumeReloadPressed()
        {
            if (!m_ReloadPressed)
                return false;

            m_ReloadPressed = false;
            return true;
        }

        public bool ConsumeFirePressed()
        {
            if (!m_FirePressed)
                return false;

            m_FirePressed = false;
            return true;
        }

        public bool ConsumeFireReleased()
        {
            if (!m_FireReleased)
                return false;

            m_FireReleased = false;
            return true;
        }

        public void SetGameplayControlsActive(bool active)
        {
            m_GameplayControlsAllowed = active;

            if (m_Canvas != null)
                UpdateVisibility();
        }

        void UpdateVisibility()
        {
            if (m_Canvas == null)
                return;

            bool shouldShow = m_GameplayControlsAllowed &&
                (ForceShowInEditor || ShouldUseMobileControls || !HideWhenNotMobile);
            m_Canvas.gameObject.SetActive(shouldShow);
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();

                foreach (BaseInputModule module in EventSystem.current.GetComponents<BaseInputModule>())
                {
                    if (module != null && !(module is InputSystemUIInputModule))
                        module.enabled = false;
                }

                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }

        void BuildUi()
        {
            if (m_Canvas != null)
                return;

            GameObject canvasObject = new GameObject("Mobile Controls Canvas");
            canvasObject.transform.SetParent(transform, false);
            m_Canvas = canvasObject.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            CreateLookArea(root);
            CreateJoystick(root);
            CreateActionButton(root, "Shoot", new Vector2(1f, 0f), new Vector2(-160f, 150f), 150f,
                () =>
                {
                    FireHeld = true;
                    m_FirePressed = true;
                },
                () =>
                {
                    FireHeld = false;
                    m_FireReleased = true;
                });
            CreateActionButton(root, "Jump", new Vector2(1f, 0f), new Vector2(-330f, 165f), 120f,
                () => m_JumpPressed = true, null);
            CreateActionButton(root, "Reload", new Vector2(1f, 0f), new Vector2(-500f, 145f), 112f,
                () => m_ReloadPressed = true, null);
            CreateActionButton(root, "Run", new Vector2(0f, 0f), new Vector2(320f, 150f), 112f,
                () => SprintHeld = true, () => SprintHeld = false);
        }

        void CreateLookArea(RectTransform parent)
        {
            GameObject lookObject = CreatePanel("Look Area", parent, new Color(1f, 1f, 1f, 0f));
            RectTransform rect = lookObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.38f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TouchRegion region = lookObject.AddComponent<TouchRegion>();
            region.OnDragDelta += delta => m_LookDelta += delta * LookSensitivity;
        }

        void CreateJoystick(RectTransform parent)
        {
            GameObject baseObject = CreatePanel("Move Joystick", parent, new Color(1f, 1f, 1f, 0.16f));
            RectTransform rect = baseObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(170f, 165f);
            rect.sizeDelta = new Vector2(JoystickRadius * 2f, JoystickRadius * 2f);

            Image baseImage = baseObject.GetComponent<Image>();
            baseImage.sprite = CreateCircleSprite();

            GameObject handleObject = CreatePanel("Handle", rect, new Color(1f, 1f, 1f, 0.42f));
            m_JoystickHandle = handleObject.GetComponent<RectTransform>();
            m_JoystickHandle.anchorMin = new Vector2(0.5f, 0.5f);
            m_JoystickHandle.anchorMax = new Vector2(0.5f, 0.5f);
            m_JoystickHandle.pivot = new Vector2(0.5f, 0.5f);
            m_JoystickHandle.sizeDelta = new Vector2(72f, 72f);
            m_JoystickHandle.anchoredPosition = Vector2.zero;
            handleObject.GetComponent<Image>().sprite = CreateCircleSprite();

            JoystickRegion joystick = baseObject.AddComponent<JoystickRegion>();
            joystick.Radius = JoystickRadius;
            joystick.OnValueChanged += value =>
            {
                MoveInput = value;
                if (m_JoystickHandle != null)
                    m_JoystickHandle.anchoredPosition = value * JoystickRadius;
            };
        }

        void CreateActionButton(RectTransform parent, string label, Vector2 anchor, Vector2 position, float size,
            System.Action onDown, System.Action onUp)
        {
            GameObject buttonObject = CreatePanel(label + " Button", parent, new Color(1f, 1f, 1f, 0.2f));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            buttonObject.GetComponent<Image>().sprite = CreateCircleSprite();

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(rect, false);
            Text text = textObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = font;
            text.fontSize = Mathf.RoundToInt(size * 0.2f);
            text.raycastTarget = false;

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TouchButton button = buttonObject.AddComponent<TouchButton>();
            button.OnPressed += onDown;
            button.OnReleased += onUp;
        }

        GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return panel;
        }

        Sprite CreateCircleSprite()
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            Vector2 center = new Vector2(31.5f, 31.5f);
            float radius = 30f;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    public class JoystickRegion : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float Radius = 86f;
        public System.Action<Vector2> OnValueChanged;

        RectTransform m_RectTransform;

        void Awake()
        {
            m_RectTransform = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnValueChanged?.Invoke(Vector2.zero);
        }

        void UpdateValue(PointerEventData eventData)
        {
            if (m_RectTransform == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                return;

            Vector2 value = Vector2.ClampMagnitude(localPoint / Radius, 1f);
            OnValueChanged?.Invoke(value);
        }
    }

    public class TouchRegion : MonoBehaviour, IDragHandler
    {
        public System.Action<Vector2> OnDragDelta;

        public void OnDrag(PointerEventData eventData)
        {
            OnDragDelta?.Invoke(eventData.delta);
        }
    }

    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action OnPressed;
        public System.Action OnReleased;

        bool m_IsPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_IsPressed)
                return;

            m_IsPressed = true;
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        void Release()
        {
            if (!m_IsPressed)
                return;

            m_IsPressed = false;
            OnReleased?.Invoke();
        }
    }
}
