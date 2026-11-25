using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Pengemudi AI sederhana: mengikuti garis tengah lintasan (dari TrackBuilder),
    /// melambat sebelum tikungan, dan reset otomatis kalau tersangkut.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class AICarDriver : MonoBehaviour
    {
        [Range(0.6f, 1.05f)]
        [Tooltip("Tingkat kesulitan: makin besar makin ngebut")]
        public float skill = 0.92f;
        [Tooltip("Geser dari garis tengah (meter, + = kanan)")]
        public float laneOffset = 0f;
        public float baseLookAhead = 14f;
        [Tooltip("Gaya samping maksimum yang dianggap aman saat tikungan (m/s^2)")]
        public float cornerGrip = 9.5f;

        CarController car;
        TrackBuilder track;
        int nearest;
        float stuckTimer;

        void Awake()
        {
            car = GetComponent<CarController>();
            car.useAIInput = true;
        }

        void Start()
        {
            track = TrackBuilder.Instance != null ? TrackBuilder.Instance : FindFirstObjectByType<TrackBuilder>();
            if (track != null && track.Count > 0) nearest = track.NearestIndex(transform.position);
        }

        void FixedUpdate()
        {
            if (track == null || track.Count == 0) return;

            if (!car.controlEnabled)
            {
                car.throttleInput = 0f;
                car.steerInput = 0f;
                car.handbrakeInput = false;
                return;
            }

            nearest = track.NearestIndex(transform.position, nearest, 30);

            float speed = car.SpeedKmh / 3.6f;
            float spacing = track.SampleSpacing;

            // --- Kemudi: arahkan ke titik di depan ---
            float look = baseLookAhead + speed * 0.45f;
            int ahead = Mathf.CeilToInt(look / spacing);
            Vector3 target = track.PointAt(nearest + ahead) + track.RightAt(nearest + ahead) * laneOffset;
            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            car.steerInput = Mathf.Clamp(angle / 22f, -1f, 1f);

            // --- Kecepatan target dari kelengkungan di depan ---
            float vMax = car.maxSpeedKmh * 0.97f * skill / 3.6f;
            float targetSpeed = vMax;
            float[] probes = { 25f, 45f, 70f, 100f };
            Vector3 t0 = track.TangentAt(nearest);
            foreach (float d in probes)
            {
                int steps = Mathf.RoundToInt(d / spacing);
                Vector3 t1 = track.TangentAt(nearest + steps);
                float turnRad = Vector3.Angle(t0, t1) * Mathf.Deg2Rad;
                float curvature = turnRad / d;
                if (curvature < 0.0005f) continue;
                float safe = Mathf.Sqrt(cornerGrip * skill / curvature);
                // boleh ngebut sampai mendekati tikungan: kurangi batas jika jaraknya masih jauh
                safe += Mathf.Max(0f, d - 25f) * 0.12f;
                targetSpeed = Mathf.Min(targetSpeed, safe);
            }

            float diff = targetSpeed - speed;
            car.throttleInput = Mathf.Clamp(diff / 5f, -1f, 1f);
            if (Mathf.Abs(car.steerInput) > 0.85f && speed > 18f) car.throttleInput = Mathf.Min(car.throttleInput, 0.4f);
            car.handbrakeInput = false;

            // --- Tersangkut? ---
            if (speed < 1.2f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 3.5f)
                {
                    stuckTimer = 0f;
                    car.ResetToCheckpoint();
                }
            }
            else stuckTimer = 0f;
        }
    }
}
