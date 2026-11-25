using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Kontrol mobil berbasis WheelCollider (penggerak roda belakang).
    /// Kontrol pemain: W/S atau panah atas/bawah = gas/rem/mundur,
    /// A/D atau panah kiri/kanan = belok, Spasi = rem tangan, R = reset ke checkpoint.
    /// Script AICarDriver memakai field throttleInput / steerInput yang sama.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Header("Wheel Colliders")]
        public WheelCollider wheelFL;
        public WheelCollider wheelFR;
        public WheelCollider wheelRL;
        public WheelCollider wheelRR;

        [Header("Wheel Meshes (visual)")]
        public Transform meshFL;
        public Transform meshFR;
        public Transform meshRL;
        public Transform meshRR;

        [Header("Mesin")]
        [Tooltip("Torsi maksimum per roda penggerak (Nm)")]
        public float maxMotorTorque = 1700f;
        public float maxSpeedKmh = 190f;
        public float reverseMaxSpeedKmh = 40f;
        public float brakeTorque = 4200f;
        public float handbrakeTorque = 7000f;
        [Tooltip("Rem mesin saat gas dilepas")]
        public float coastBrake = 180f;
        public bool allWheelDrive = false;

        [Header("Kemudi")]
        public float maxSteerAngle = 30f;
        public float highSpeedSteerAngle = 6f;
        public float steerSpeedKmh = 160f;
        public float steerSmoothing = 8f;

        [Header("Stabilitas")]
        public float downforce = 40f;
        public float antiRoll = 6000f;
        public Vector3 centerOfMass = new Vector3(0f, 0.35f, 0.1f);
        [Tooltip("Grip roda di rumput (1 = sama seperti aspal)")]
        [Range(0.2f, 1f)] public float offRoadGrip = 0.55f;
        public float offRoadDrag = 0.35f;
        [Tooltip("Pengali grip sisi roda belakang saat rem tangan (untuk drift)")]
        [Range(0.2f, 1f)] public float handbrakeRearGrip = 0.45f;

        [Header("Input (diisi pemain atau AI)")]
        public bool useAIInput = false;
        public bool controlEnabled = true;
        [Range(-1f, 1f)] public float throttleInput;
        [Range(-1f, 1f)] public float steerInput;
        public bool handbrakeInput;

        // --- info untuk HUD / audio ---
        public float SpeedKmh { get; private set; }
        public float ForwardSpeedKmh { get; private set; }
        public int Gear { get; private set; } = 1;     // -1 = mundur
        public float Rpm01 { get; private set; }       // 0..1
        public bool IsReversing => ForwardSpeedKmh < -2f;

        Rigidbody rb;
        RacerProgress progress;
        float currentSteer;
        float flipTimer;

        Quaternion offFL, offFR, offRL, offRR;

        WheelFrictionCurve baseFwd, baseSide;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            progress = GetComponent<RacerProgress>();
            rb.centerOfMass = centerOfMass;

            if (wheelFL != null)
            {
                baseFwd = wheelFL.forwardFriction;
                baseSide = wheelFL.sidewaysFriction;
            }

            offFL = ComputeOffset(wheelFL, meshFL);
            offFR = ComputeOffset(wheelFR, meshFR);
            offRL = ComputeOffset(wheelRL, meshRL);
            offRR = ComputeOffset(wheelRR, meshRR);
        }

        static Quaternion ComputeOffset(WheelCollider c, Transform m)
        {
            if (c == null || m == null) return Quaternion.identity;
            c.GetWorldPose(out _, out Quaternion q);
            return Quaternion.Inverse(q) * m.rotation;
        }

        void Update()
        {
            if (!useAIInput) ReadPlayerInput();
            UpdateWheelVisual(wheelFL, meshFL, offFL);
            UpdateWheelVisual(wheelFR, meshFR, offFR);
            UpdateWheelVisual(wheelRL, meshRL, offRL);
            UpdateWheelVisual(wheelRR, meshRR, offRR);
            UpdateGearAndRpm();
        }

        void FixedUpdate()
        {
            Vector3 vel = PhysCompat.GetVelocity(rb);
            SpeedKmh = vel.magnitude * 3.6f;
            ForwardSpeedKmh = Vector3.Dot(vel, transform.forward) * 3.6f;

            Drive();
            UpdateSurfaceAndFriction();
            AntiRollBar(wheelFL, wheelFR);
            AntiRollBar(wheelRL, wheelRR);
            rb.AddForce(-transform.up * downforce * vel.magnitude);
            CheckFlipped();
        }

        // ------------------------------------------------------------------
        void ReadPlayerInput()
        {
            throttleInput = Input.GetAxis("Vertical");
            steerInput = Input.GetAxis("Horizontal");
            handbrakeInput = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.R)) ResetToCheckpoint();
        }

        void Drive()
        {
            float throttle = controlEnabled ? throttleInput : 0f;
            float steer = controlEnabled ? steerInput : 0f;
            bool hand = controlEnabled && handbrakeInput;

            float motor = 0f;
            float brake = 0f;
            float fwd = ForwardSpeedKmh / 3.6f;

            if (!controlEnabled)
            {
                brake = brakeTorque;      // mobil ditahan (sebelum start / setelah finis)
            }
            else if (throttle > 0.01f)
            {
                if (fwd < -1f) brake = brakeTorque * throttle;
                else motor = throttle * maxMotorTorque * TorqueCurve(SpeedKmh / maxSpeedKmh);
            }
            else if (throttle < -0.01f)
            {
                if (fwd > 1f) brake = brakeTorque * -throttle;
                else if (SpeedKmh < reverseMaxSpeedKmh) motor = throttle * maxMotorTorque * 0.5f;
            }
            else
            {
                brake = coastBrake;
            }

            wheelRL.motorTorque = motor;
            wheelRR.motorTorque = motor;
            wheelFL.motorTorque = allWheelDrive ? motor : 0f;
            wheelFR.motorTorque = allWheelDrive ? motor : 0f;

            wheelFL.brakeTorque = brake;
            wheelFR.brakeTorque = brake;
            float rearBrake = brake + (hand ? handbrakeTorque : 0f);
            wheelRL.brakeTorque = rearBrake;
            wheelRR.brakeTorque = rearBrake;

            // Kemudi: makin cepat, makin kecil sudut belok maksimum
            float t = Mathf.Clamp01(SpeedKmh / steerSpeedKmh);
            float target = steer * Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, t);
            currentSteer = Mathf.Lerp(currentSteer, target, steerSmoothing * Time.fixedDeltaTime);
            wheelFL.steerAngle = currentSteer;
            wheelFR.steerAngle = currentSteer;
        }

        static float TorqueCurve(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        // ------------------------------------------------------------------
        // Rumput = grip kecil + hambatan besar. Rem tangan = roda belakang licin.
        void UpdateSurfaceAndFriction()
        {
            int offRoadWheels = 0;
            offRoadWheels += ApplyFriction(wheelFL, false);
            offRoadWheels += ApplyFriction(wheelFR, false);
            offRoadWheels += ApplyFriction(wheelRL, true);
            offRoadWheels += ApplyFriction(wheelRR, true);

            float frac = offRoadWheels / 4f;
            PhysCompat.SetDrag(rb, Mathf.Lerp(0.02f, offRoadDrag, frac));
        }

        int ApplyFriction(WheelCollider w, bool rear)
        {
            bool off = false;
            if (w.GetGroundHit(out WheelHit hit) && hit.collider != null)
                off = hit.collider.name.Contains("Grass");

            float surface = off ? offRoadGrip : 1f;
            float sideMul = surface;
            if (rear && controlEnabled && handbrakeInput) sideMul *= handbrakeRearGrip;

            WheelFrictionCurve f = baseFwd;
            f.stiffness = baseFwd.stiffness * surface;
            w.forwardFriction = f;

            WheelFrictionCurve s = baseSide;
            s.stiffness = baseSide.stiffness * sideMul;
            w.sidewaysFriction = s;

            return off ? 1 : 0;
        }

        void AntiRollBar(WheelCollider l, WheelCollider r)
        {
            float travelL = 1f, travelR = 1f;
            bool groundedL = l.GetGroundHit(out WheelHit hit);
            if (groundedL)
                travelL = (-l.transform.InverseTransformPoint(hit.point).y - l.radius) / l.suspensionDistance;

            bool groundedR = r.GetGroundHit(out hit);
            if (groundedR)
                travelR = (-r.transform.InverseTransformPoint(hit.point).y - r.radius) / r.suspensionDistance;

            float force = (travelL - travelR) * antiRoll;
            if (groundedL) rb.AddForceAtPosition(l.transform.up * -force, l.transform.position);
            if (groundedR) rb.AddForceAtPosition(r.transform.up * force, r.transform.position);
        }

        // ------------------------------------------------------------------
        static void UpdateWheelVisual(WheelCollider c, Transform m, Quaternion offset)
        {
            if (c == null || m == null) return;
            c.GetWorldPose(out Vector3 pos, out Quaternion rot);
            m.SetPositionAndRotation(pos, rot * offset);
        }

        void UpdateGearAndRpm()
        {
            const int gears = 6;
            float speed = Mathf.Abs(ForwardSpeedKmh);
            float band = maxSpeedKmh / gears;
            if (ForwardSpeedKmh < -2f)
            {
                Gear = -1;
                Rpm01 = Mathf.Lerp(Rpm01, Mathf.Lerp(0.25f, 0.9f, speed / reverseMaxSpeedKmh), Time.deltaTime * 8f);
                return;
            }
            int g = Mathf.Clamp(Mathf.FloorToInt(speed / band) + 1, 1, gears);
            Gear = g;
            float frac = Mathf.Clamp01((speed - (g - 1) * band) / band);
            float target = Mathf.Lerp(0.3f, 1f, frac);
            if (controlEnabled && Mathf.Abs(throttleInput) > 0.05f && speed < 3f)
                target = Mathf.Max(target, 0.45f);   // revving saat start
            Rpm01 = Mathf.Lerp(Rpm01, target, Time.deltaTime * 10f);
        }

        // ------------------------------------------------------------------
        void CheckFlipped()
        {
            bool flipped = Vector3.Dot(transform.up, Vector3.up) < 0.25f && SpeedKmh < 4f;
            flipTimer = flipped ? flipTimer + Time.fixedDeltaTime : 0f;
            if (flipTimer > 2.5f)
            {
                flipTimer = 0f;
                ResetToCheckpoint();
            }
        }

        /// <summary>Kembalikan mobil ke checkpoint terakhir (tombol R / otomatis jika terbalik).</summary>
        public void ResetToCheckpoint()
        {
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            if (progress != null)
            {
                pos = progress.respawnPosition;
                rot = progress.respawnRotation;
            }
            else
            {
                rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }

            PhysCompat.SetVelocity(rb, Vector3.zero);
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(pos + Vector3.up * 0.6f, rot);
            currentSteer = 0f;
        }
    }
}
