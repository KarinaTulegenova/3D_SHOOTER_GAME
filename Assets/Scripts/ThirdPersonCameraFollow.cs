using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class ThirdPersonCameraFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0.55f, 1.75f, -2.7f);
        public Vector3 LookOffset = new Vector3(0f, 1.2f, 0f);
        public float PositionSharpness = 12f;
        public float RotationSharpness = 12f;
        public float CollisionRadius = 0.25f;
        public float MinDistanceFromTarget = 1.2f;
        public LayerMask CollisionLayers = -1;

        Collider[] m_TargetColliders;

        void Start()
        {
            CacheTargetColliders();
        }

        void LateUpdate()
        {
            if (Target == null)
                return;

            if (m_TargetColliders == null || m_TargetColliders.Length == 0)
                CacheTargetColliders();

            Vector3 lookPoint = Target.position + LookOffset;
            Vector3 desiredPosition = Target.TransformPoint(Offset);
            desiredPosition = ResolveCameraCollision(lookPoint, desiredPosition);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * PositionSharpness);

            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * RotationSharpness);
        }

        Vector3 ResolveCameraCollision(Vector3 lookPoint, Vector3 desiredPosition)
        {
            Vector3 lookToCamera = desiredPosition - lookPoint;
            float desiredDistance = lookToCamera.magnitude;

            if (desiredDistance <= MinDistanceFromTarget)
                return desiredPosition;

            Vector3 direction = lookToCamera / desiredDistance;
            if (Physics.SphereCast(lookPoint, CollisionRadius, direction, out RaycastHit hit, desiredDistance,
                    CollisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsTargetCollider(hit.collider))
                    return desiredPosition;

                float safeDistance = Mathf.Max(hit.distance - CollisionRadius, MinDistanceFromTarget);
                return lookPoint + direction * safeDistance;
            }

            return desiredPosition;
        }

        void CacheTargetColliders()
        {
            if (Target != null)
                m_TargetColliders = Target.GetComponentsInChildren<Collider>(true);
        }

        bool IsTargetCollider(Collider hitCollider)
        {
            if (hitCollider == null || m_TargetColliders == null)
                return false;

            foreach (Collider targetCollider in m_TargetColliders)
            {
                if (targetCollider == hitCollider)
                    return true;
            }

            return false;
        }
    }
}
