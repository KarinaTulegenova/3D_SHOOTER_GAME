using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.FPS.Gameplay
{
    public class CameraSwitch : MonoBehaviour
    {
        [Header("Cameras")]
        public Camera FirstPersonCamera;
        public Camera ThirdPersonCamera;
        public Camera WeaponCamera;

        [Header("Input")]
        public Key SwitchKey = Key.V;

        [Header("First Person Objects")]
        public GameObject[] FirstPersonOnlyObjects;

        [Header("Third Person Objects")]
        public GameObject[] ThirdPersonOnlyObjects;

        bool m_UsingThirdPerson;
        PlayerWeaponsManager m_WeaponsManager;
        Camera m_FirstPersonWeaponCamera;

        void Start()
        {
            AutoFindReferences();
            SetThirdPerson(false);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current[SwitchKey].wasPressedThisFrame)
                SetThirdPerson(!m_UsingThirdPerson);
        }

        public void SetThirdPerson(bool useThirdPerson)
        {
            AutoFindReferences();
            m_UsingThirdPerson = useThirdPerson;

            if (FirstPersonCamera != null)
            {
                FirstPersonCamera.enabled = !m_UsingThirdPerson;
                FirstPersonCamera.tag = m_UsingThirdPerson ? "Untagged" : "MainCamera";
            }

            if (ThirdPersonCamera != null)
            {
                ThirdPersonCamera.enabled = m_UsingThirdPerson;
                ThirdPersonCamera.tag = m_UsingThirdPerson ? "MainCamera" : "Untagged";
            }

            if (WeaponCamera != null)
                WeaponCamera.enabled = !m_UsingThirdPerson;

            if (m_WeaponsManager != null)
            {
                m_WeaponsManager.WeaponCamera = m_UsingThirdPerson ? ThirdPersonCamera : m_FirstPersonWeaponCamera;
                SetObjectRenderers(m_WeaponsManager.WeaponParentSocket != null
                    ? m_WeaponsManager.WeaponParentSocket.gameObject
                    : null, !m_UsingThirdPerson);
            }

            foreach (GameObject firstPersonObject in FirstPersonOnlyObjects)
            {
                if (firstPersonObject != null)
                    firstPersonObject.SetActive(!m_UsingThirdPerson);
            }

            foreach (GameObject thirdPersonObject in ThirdPersonOnlyObjects)
            {
                if (thirdPersonObject != null)
                {
                    thirdPersonObject.SetActive(m_UsingThirdPerson);
                    SetObjectRenderers(thirdPersonObject, m_UsingThirdPerson);
                }
            }

            SetAudioListener(FirstPersonCamera, !m_UsingThirdPerson);
            SetAudioListener(ThirdPersonCamera, m_UsingThirdPerson);

            ThirdPersonPlayerAnimator thirdPersonAnimator = GetComponent<ThirdPersonPlayerAnimator>();
            if (thirdPersonAnimator != null)
                thirdPersonAnimator.OnThirdPersonVisualToggled(m_UsingThirdPerson);
        }

        void AutoFindReferences()
        {
            if (FirstPersonCamera == null)
                FirstPersonCamera = Camera.main;

            if (WeaponCamera == null)
            {
                Transform weaponCameraTransform = transform.Find("Main Camera/WeaponCamera");
                if (weaponCameraTransform != null)
                    WeaponCamera = weaponCameraTransform.GetComponent<Camera>();
            }

            if (m_FirstPersonWeaponCamera == null)
                m_FirstPersonWeaponCamera = WeaponCamera;

            if (ThirdPersonCamera == null)
            {
                Transform thirdPersonTransform = transform.Find("ThirdPersonCamera");
                if (thirdPersonTransform != null)
                    ThirdPersonCamera = thirdPersonTransform.GetComponent<Camera>();
            }

            if (m_WeaponsManager == null)
                m_WeaponsManager = GetComponent<PlayerWeaponsManager>();

            if (ThirdPersonOnlyObjects == null || ThirdPersonOnlyObjects.Length == 0 || ThirdPersonOnlyObjects[0] == null)
            {
                Transform thirdPersonModel = transform.Find("ThirdPersonPlayerModel");
                if (thirdPersonModel != null)
                    ThirdPersonOnlyObjects = new[] { thirdPersonModel.gameObject };
            }
        }

        void SetAudioListener(Camera targetCamera, bool isEnabled)
        {
            if (targetCamera == null)
                return;

            AudioListener listener = targetCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = isEnabled;
        }

        void SetObjectRenderers(GameObject targetObject, bool isVisible)
        {
            if (targetObject == null)
                return;

            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer != null)
                    targetRenderer.enabled = isVisible;
            }
        }
    }
}
