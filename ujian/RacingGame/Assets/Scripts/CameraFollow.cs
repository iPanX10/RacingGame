using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Kamera mengikuti mobil. Tombol C = ganti sudut kamera (dekat / jauh / kap mesin).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;

        [Header("Kamera Belakang")]
        public float distance = 6.5f;
        public float height = 2.6f;
        public float lookAhead = 4f;
        public float lookHeight = 1.1f;
        public float positionSmooth = 6f;
        public float rotationSmooth = 9f;

        [Header("FOV")]
        public float baseFov = 62f;
        public float maxFov = 82f;
        public float maxFovSpeedKmh = 200f;

        Camera cam;
        CarController car;
        int mode;   // 0 dekat, 1 jauh, 2 kap mesin

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (target != null) car = target.GetComponent<CarController>();
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (Input.GetKeyDown(KeyCode.C)) mode = (mode + 1) % 3;

            float speed = car != null ? car.SpeedKmh : 0f;
            float fovTarget = Mathf.Lerp(baseFov, maxFov, Mathf.Clamp01(speed / maxFovSpeedKmh));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovTarget, Time.deltaTime * 4f);

            if (mode == 2)
            {
                // Kamera kap mesin: menempel di mobil
                transform.position = target.TransformPoint(new Vector3(0f, 1.05f, 0.35f));
                transform.rotation = target.rotation;
                return;
            }

            float d = mode == 0 ? distance : distance * 1.8f;
            float h = mode == 0 ? height : height * 1.9f;

            Vector3 fwd = target.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 desired = target.position - fwd * d + Vector3.up * h;
            float pk = 1f - Mathf.Exp(-positionSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, pk);

            Vector3 lookPoint = target.position + fwd * lookAhead + Vector3.up * lookHeight;
            Quaternion wanted = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            float rk = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, rk);
        }

        /// <summary>Langsung pindah ke belakang target (dipanggil saat setup).</summary>
        public void Snap()
        {
            if (target == null) return;
            Vector3 fwd = target.forward; fwd.y = 0f; fwd.Normalize();
            transform.position = target.position - fwd * distance + Vector3.up * height;
            transform.LookAt(target.position + fwd * lookAhead + Vector3.up * lookHeight);
        }
    }
}
