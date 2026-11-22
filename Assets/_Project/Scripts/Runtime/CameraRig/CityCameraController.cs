using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Logistics
{
    /// <summary>
    /// SimCity-style rig: this transform is the Cinemachine follow target at the focus point on the ground;
    /// its rotation (pitch/yaw) and the follow offset (distance) frame the city. WASD/arrows or middle-drag
    /// pans, the wheel zooms, Q/E rotates in 90° steps. Also keeps the tilt-shift DoF focused on the target.
    /// </summary>
    public class CityCameraController : MonoBehaviour
    {
        public CinemachineCamera vcam;
        public Volume volume;
        public float minDistance = 45f;
        public float maxDistance = 330f;
        public float distance = 150f;
        public float yaw = 0f;
        public bool inputEnabled = true;
        public bool orbit;

        CinemachineFollow follow;
        float targetDistance, targetYaw;
        Vector3 targetPos;
        Bounds limits;
        DepthOfField dof;
        Vector2 lastMouse;
        bool dragging;
        Vector3 shakeOffset;

        void Awake()
        {
            targetDistance = distance;
            targetYaw = yaw;
            targetPos = transform.position;
            if (vcam != null) follow = vcam.GetComponent<CinemachineFollow>();
            if (volume != null && volume.profile != null) volume.profile.TryGet(out dof);
        }

        public void SetLimits(Bounds b) => limits = b;

        public void Focus(Vector3 p, float dist)
        {
            targetPos = new Vector3(p.x, 0f, p.z);
            targetDistance = Mathf.Clamp(dist, minDistance, maxDistance);
        }

        public void JumpTo(Vector3 p, float dist)
        {
            Focus(p, dist);
            transform.position = targetPos;
            distance = targetDistance;
            Apply();
            if (vcam != null) vcam.PreviousStateIsValid = false;
        }

        float Pitch(float d) => Mathf.Lerp(42f, 62f, Mathf.InverseLerp(minDistance, maxDistance, d));

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (orbit)
            {
                targetYaw += dt * 4f;
            }
            else if (inputEnabled)
            {
                HandleInput(dt);
            }

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-dt * 8f));
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-dt * 9f));
            if (limits.size.sqrMagnitude > 0f)
            {
                targetPos.x = Mathf.Clamp(targetPos.x, limits.min.x, limits.max.x);
                targetPos.z = Mathf.Clamp(targetPos.z, limits.min.z, limits.max.z);
            }
            transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-dt * 10f));
            Apply();
        }

        void HandleInput(float dt)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float panSpeed = distance * 0.9f;
            if (kb != null)
            {
                var move = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += fwd;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= fwd;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += right;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= right;
                targetPos += move * panSpeed * dt;
                if (kb.qKey.wasPressedThisFrame) targetYaw -= 90f;
                if (kb.eKey.wasPressedThisFrame) targetYaw += 90f;
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float steps = Mathf.Clamp(scroll / 120f, -1f, 1f);
                    if (Mathf.Abs(scroll) < 10f) steps = Mathf.Clamp(scroll * 0.25f, -1f, 1f);
                    targetDistance = Mathf.Clamp(targetDistance * (1f - steps * 0.14f), minDistance, maxDistance);
                }
                var mp = mouse.position.ReadValue();
                if (mouse.middleButton.wasPressedThisFrame) { dragging = true; lastMouse = mp; }
                if (!mouse.middleButton.isPressed) dragging = false;
                if (dragging)
                {
                    var d = mp - lastMouse;
                    lastMouse = mp;
                    float k = distance / Mathf.Max(1f, Screen.height) * 1.1f;
                    targetPos -= (right * d.x + fwd * d.y) * k;
                }
            }
        }

        public void Shake(float strength)
        {
            var src = GetComponent<CinemachineImpulseSource>();
            if (src != null) src.GenerateImpulseWithForce(strength);
        }

        void Apply()
        {
            transform.rotation = Quaternion.Euler(Pitch(distance), yaw, 0f);
            if (follow != null) follow.FollowOffset = new Vector3(0f, 0f, -distance);
            if (dof != null)
            {
                dof.focusDistance.value = distance;
                dof.focalLength.value = Mathf.Lerp(70f, 120f, Mathf.InverseLerp(minDistance, maxDistance, distance));
            }
        }
    }
}
