using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// HUD memakai OnGUI (tanpa Canvas/TextMeshPro, jadi tidak perlu setup UI):
    /// kecepatan, gigi, RPM, lap, posisi, waktu, hitung mundur, hasil akhir, dan pause (Esc).
    /// </summary>
    public class RaceHUD : MonoBehaviour
    {
        GUIStyle big, medium, small, center;
        Texture2D bg, white;
        bool paused;

        void Awake()
        {
            bg = MakeTex(new Color(0f, 0f, 0f, 0.55f));
            white = MakeTex(Color.white);
        }

        static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
            }
            if (paused && Input.GetKeyDown(KeyCode.Q))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        void InitStyles()
        {
            float s = Screen.height / 720f;
            big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(54 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            medium = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * s), alignment = TextAnchor.MiddleLeft };
            center = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(110 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = medium.normal.textColor = small.normal.textColor = center.normal.textColor = Color.white;
        }

        static string Fmt(float t)
        {
            if (t <= 0f || t >= float.MaxValue) return "--:--.---";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return string.Format("{0:00}:{1:00.000}", m, s);
        }

        static string Ordinal(int n)
        {
            return n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");
        }

        void OnGUI()
        {
            LapManager lm = LapManager.Instance;
            if (lm == null || lm.Player == null) return;
            if (big == null) InitStyles();

            float s = Screen.height / 720f;
            RacerProgress p = lm.Player;
            CarController car = p.GetComponent<CarController>();
            int total = lm.Racers.Count;

            // ---------- Kiri atas: lap / posisi / waktu ----------
            GUI.DrawTexture(new Rect(16 * s, 16 * s, 330 * s, 150 * s), bg);
            GUI.Label(new Rect(30 * s, 20 * s, 310 * s, 40 * s), "LAP  " + Mathf.Min(p.lap, lm.totalLaps) + " / " + lm.totalLaps, medium);
            GUI.Label(new Rect(30 * s, 56 * s, 310 * s, 40 * s), "POS  " + Ordinal(lm.GetRank(p)) + " / " + total, medium);
            GUI.Label(new Rect(30 * s, 96 * s, 310 * s, 26 * s), "Waktu  " + Fmt(lm.RaceTime), small);
            GUI.Label(new Rect(30 * s, 118 * s, 310 * s, 26 * s), "Lap terakhir  " + Fmt(p.lastLapTime) + "   Terbaik  " + Fmt(p.bestLapTime), small);
            GUI.Label(new Rect(30 * s, 140 * s, 310 * s, 26 * s), "Rekor tersimpan  " + Fmt(lm.PlayerBestLapSaved), small);

            // ---------- Kanan atas: klasemen ----------
            List<RacerProgress> st = lm.GetStandings();
            float h = (st.Count * 24 + 16) * s;
            GUI.DrawTexture(new Rect(Screen.width - 240 * s - 16 * s, 16 * s, 240 * s, h), bg);
            for (int i = 0; i < st.Count; i++)
            {
                string line = (i + 1) + ".  " + st[i].racerName + (st[i].finished ? "   (FINISH)" : "");
                Color old = small.normal.textColor;
                small.normal.textColor = st[i].isPlayer ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUI.Label(new Rect(Screen.width - 226 * s, (24 + i * 24) * s, 220 * s, 24 * s), line, small);
                small.normal.textColor = old;
            }

            // ---------- Kanan bawah: speedometer ----------
            if (car != null)
            {
                float bx = Screen.width - 320 * s - 16 * s;
                float by = Screen.height - 150 * s - 16 * s;
                GUI.DrawTexture(new Rect(bx, by, 320 * s, 150 * s), bg);
                GUI.Label(new Rect(bx + 10 * s, by + 8 * s, 220 * s, 70 * s), Mathf.RoundToInt(car.SpeedKmh).ToString(), big);
                GUI.Label(new Rect(bx + 235 * s, by + 28 * s, 80 * s, 30 * s), "km/h", small);
                string gear = car.Gear < 0 ? "R" : car.Gear.ToString();
                GUI.Label(new Rect(bx + 235 * s, by + 4 * s, 80 * s, 30 * s), "GIGI " + gear, small);

                // bar RPM
                float barW = 296 * s;
                GUI.DrawTexture(new Rect(bx + 12 * s, by + 92 * s, barW, 16 * s), bg);
                Color old = GUI.color;
                GUI.color = Color.Lerp(new Color(0.2f, 1f, 0.3f), new Color(1f, 0.15f, 0.1f), Mathf.Clamp01((car.Rpm01 - 0.7f) / 0.3f));
                GUI.DrawTexture(new Rect(bx + 12 * s, by + 92 * s, barW * car.Rpm01, 16 * s), white);
                GUI.color = old;
                GUI.Label(new Rect(bx + 12 * s, by + 114 * s, 300 * s, 26 * s), "R = reset  |  C = kamera  |  Spasi = rem tangan", small);
            }

            // ---------- Tengah: hitung mundur / GO ----------
            if (lm.State == RaceState.Countdown && lm.CountdownNumber > 0)
            {
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 160 * s), lm.CountdownNumber.ToString(), center);
            }
            else if (lm.State == RaceState.Racing && Time.time < lm.GoMessageUntil)
            {
                Color old = center.normal.textColor;
                center.normal.textColor = new Color(0.3f, 1f, 0.4f);
                GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 160 * s), "GO!", center);
                center.normal.textColor = old;
            }

            // ---------- Hasil akhir ----------
            if (lm.State == RaceState.Finished)
            {
                float w = 460 * s, hh = 330 * s;
                Rect r = new Rect((Screen.width - w) / 2f, (Screen.height - hh) / 2f, w, hh);
                GUI.DrawTexture(r, bg);
                GUI.Label(new Rect(r.x + 20 * s, r.y + 10 * s, w - 40 * s, 44 * s), "FINISH!  " + Ordinal(p.finishPosition), medium);
                GUI.Label(new Rect(r.x + 20 * s, r.y + 56 * s, w - 40 * s, 28 * s), "Total waktu  " + Fmt(p.finishTime) + "     Lap terbaik  " + Fmt(p.bestLapTime), small);
                for (int i = 0; i < st.Count; i++)
                {
                    string t = st[i].finished ? Fmt(st[i].finishTime) : "belum finis";
                    GUI.Label(new Rect(r.x + 20 * s, r.y + (96 + i * 28) * s, w - 40 * s, 28 * s), (i + 1) + ".  " + st[i].racerName + "   " + t, small);
                }
                GUI.Label(new Rect(r.x + 20 * s, r.y + hh - 40 * s, w - 40 * s, 28 * s), "Tekan ENTER untuk balapan lagi", small);
            }

            // ---------- Pause ----------
            if (paused)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bg);
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 100 * s), "PAUSE", center);
                GUIStyle c = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 40 * s), "Esc = lanjut   |   Q = keluar", c);
            }
        }
    }
}
