using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Trigger yang dilewati mobil. Checkpoint 0 = garis start/finish.
    /// Dibuat otomatis oleh TrackBuilder.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : MonoBehaviour
    {
        public int index;

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            RacerProgress racer = other.GetComponentInParent<RacerProgress>();
            if (racer == null || LapManager.Instance == null) return;
            LapManager.Instance.CheckpointPassed(racer, this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = index == 0 ? new Color(1f, 1f, 1f, 0.6f) : new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            BoxCollider b = GetComponent<BoxCollider>();
            if (b != null) Gizmos.DrawCube(b.center, b.size);
        }
    }
}
