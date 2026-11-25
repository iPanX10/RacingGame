using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RacingGame
{
    public enum RaceState { Countdown, Racing, Finished }

    /// <summary>
    /// Mengatur jalannya balapan: hitung mundur, checkpoint berurutan, lap,
    /// posisi (ranking), waktu terbaik, dan restart.
    /// </summary>
    public class LapManager : MonoBehaviour
    {
        public static LapManager Instance { get; private set; }

        [Header("Aturan")]
        public int totalLaps = 3;
        public int countdownFrom = 3;

        public RaceState State { get; private set; } = RaceState.Countdown;
        public int CountdownNumber { get; private set; }     // 3,2,1 ; 0 = "GO!"
        public float RaceTime => State == RaceState.Countdown ? 0f : Time.time - raceStartTime;
        public float GoMessageUntil { get; private set; }
        public RacerProgress Player { get; private set; }
        public IReadOnlyList<RacerProgress> Racers => racers;
        public float PlayerBestLapSaved { get; private set; }

        readonly List<RacerProgress> racers = new List<RacerProgress>();
        Checkpoint[] checkpoints;
        float raceStartTime;
        int finishedCount;
        const string BestLapKey = "RacingGame.BestLap";

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            System.Array.Sort(checkpoints, (a, b) => a.index.CompareTo(b.index));

            foreach (RacerProgress r in FindObjectsByType<RacerProgress>(FindObjectsSortMode.None))
            {
                racers.Add(r);
                if (r.isPlayer) Player = r;
            }

            PlayerBestLapSaved = PlayerPrefs.GetFloat(BestLapKey, 0f);
            StartCoroutine(RaceRoutine());
        }

        IEnumerator RaceRoutine()
        {
            State = RaceState.Countdown;
            SetAllControl(false);
            yield return new WaitForSeconds(1.0f);

            for (int n = countdownFrom; n >= 1; n--)
            {
                CountdownNumber = n;
                yield return new WaitForSeconds(1f);
            }

            CountdownNumber = 0;
            GoMessageUntil = Time.time + 1.2f;
            State = RaceState.Racing;
            raceStartTime = Time.time;
            foreach (RacerProgress r in racers) r.lapStartTime = Time.time;
            SetAllControl(true);
        }

        void SetAllControl(bool enabled)
        {
            foreach (RacerProgress r in racers)
            {
                CarController c = r.GetComponent<CarController>();
                if (c != null) c.controlEnabled = enabled;
            }
        }

        // ------------------------------------------------------------------
        public void CheckpointPassed(RacerProgress racer, Checkpoint cp)
        {
            if (State == RaceState.Countdown || racer.finished) return;
            if (cp.index != racer.nextCheckpoint) return;   // harus berurutan (anti-curang)

            racer.respawnPosition = cp.transform.position;
            racer.respawnRotation = Quaternion.Euler(0f, cp.transform.eulerAngles.y, 0f);

            if (cp.index == 0)
            {
                // Satu lap selesai
                float lapTime = Time.time - racer.lapStartTime;
                racer.lastLapTime = lapTime;
                if (lapTime < racer.bestLapTime) racer.bestLapTime = lapTime;
                racer.lapStartTime = Time.time;

                if (racer.isPlayer && (PlayerBestLapSaved <= 0f || lapTime < PlayerBestLapSaved))
                {
                    PlayerBestLapSaved = lapTime;
                    PlayerPrefs.SetFloat(BestLapKey, lapTime);
                    PlayerPrefs.Save();
                }

                racer.lap++;
                if (racer.lap > totalLaps)
                {
                    FinishRacer(racer);
                    return;
                }
                racer.nextCheckpoint = 1;
            }
            else
            {
                racer.nextCheckpoint = (cp.index + 1) % checkpoints.Length;
            }
        }

        void FinishRacer(RacerProgress racer)
        {
            racer.finished = true;
            racer.lap = totalLaps;
            racer.finishTime = Time.time - raceStartTime;
            racer.finishPosition = ++finishedCount;

            CarController car = racer.GetComponent<CarController>();
            if (car != null) car.controlEnabled = false;

            if (racer.isPlayer) State = RaceState.Finished;
        }

        // ------------------------------------------------------------------
        /// <summary>Posisi balapan (1 = terdepan).</summary>
        public int GetRank(RacerProgress racer)
        {
            int rank = 1;
            float mine = Score(racer);
            foreach (RacerProgress r in racers)
                if (r != racer && Score(r) > mine) rank++;
            return rank;
        }

        float Score(RacerProgress r)
        {
            if (r.finished) return 1e9f - r.finishPosition;
            if (checkpoints == null || checkpoints.Length == 0) return 0f;
            int n = checkpoints.Length;
            int passed = (r.lap - 1) * n + (r.nextCheckpoint == 0 ? n : r.nextCheckpoint);
            float dist = Vector3.Distance(r.transform.position, checkpoints[r.nextCheckpoint].transform.position);
            return passed * 10000f - dist;
        }

        public List<RacerProgress> GetStandings()
        {
            List<RacerProgress> list = new List<RacerProgress>(racers);
            list.Sort((a, b) => Score(b).CompareTo(Score(a)));
            return list;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            if (State == RaceState.Finished && Input.GetKeyDown(KeyCode.Return)) Restart();
        }
    }
}
