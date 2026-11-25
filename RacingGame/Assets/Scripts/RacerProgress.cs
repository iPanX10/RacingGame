using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Menyimpan progres balapan satu mobil (pemain maupun AI):
    /// lap, checkpoint berikutnya, waktu lap, dan titik respawn.
    /// </summary>
    public class RacerProgress : MonoBehaviour
    {
        public string racerName = "Racer";
        public bool isPlayer = false;

        [HideInInspector] public int lap = 1;
        [HideInInspector] public int nextCheckpoint = 1;
        [HideInInspector] public float lapStartTime;
        [HideInInspector] public float lastLapTime;
        [HideInInspector] public float bestLapTime = float.MaxValue;
        [HideInInspector] public bool finished;
        [HideInInspector] public float finishTime;
        [HideInInspector] public int finishPosition;

        [HideInInspector] public Vector3 respawnPosition;
        [HideInInspector] public Quaternion respawnRotation;

        public bool HasBestLap => bestLapTime < float.MaxValue;

        void Awake()
        {
            // Titik respawn awal = posisi di grid start
            respawnPosition = transform.position;
            respawnRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }
    }
}
