using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Suara mesin sintetis (tanpa file audio): gelombang bergigi + harmonik,
    /// pitch mengikuti RPM dari CarController.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(CarController))]
    public class EngineSound : MonoBehaviour
    {
        public float minPitch = 0.55f;
        public float maxPitch = 2.1f;
        public float volume = 0.3f;

        AudioSource src;
        CarController car;

        void Awake()
        {
            car = GetComponent<CarController>();
            src = GetComponent<AudioSource>();
            src.clip = BuildClip();
            src.loop = true;
            src.playOnAwake = false;
            src.dopplerLevel = 0f;
            src.volume = volume;

            RacerProgress rp = GetComponent<RacerProgress>();
            bool player = rp != null && rp.isPlayer;
            src.spatialBlend = player ? 0f : 1f;
            src.minDistance = 8f;
            src.maxDistance = 90f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
        }

        static AudioClip BuildClip()
        {
            // 10 siklus @ 100 Hz => loop mulus tanpa "klik"
            const int sampleRate = 44100;
            const int cycles = 10;
            const int length = 4410;
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float phase = 2f * Mathf.PI * cycles * i / length;
                float s = 0f;
                for (int h = 1; h <= 8; h++) s += Mathf.Sin(phase * h) / h;
                data[i] = (float)System.Math.Tanh(s * 1.3f) * 0.6f;
            }
            AudioClip clip = AudioClip.Create("EngineLoop", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Update()
        {
            float rpm = car.Rpm01;
            src.pitch = Mathf.Lerp(minPitch, maxPitch, rpm);
            float load = car.controlEnabled ? Mathf.Abs(car.throttleInput) : 0.3f;
            src.volume = Mathf.Lerp(volume * 0.6f, volume, Mathf.Max(load, rpm * 0.5f));
        }
    }
}
