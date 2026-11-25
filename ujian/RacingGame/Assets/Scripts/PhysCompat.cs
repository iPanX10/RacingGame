using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Pembungkus kecil supaya script jalan di Unity 2022 LTS maupun Unity 6
    /// (di Unity 6 Rigidbody.velocity diganti linearVelocity, drag -> linearDamping).
    /// </summary>
    public static class PhysCompat
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDrag(Rigidbody rb, float drag)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = drag;
#else
            rb.drag = drag;
#endif
        }

        public static void SetAngularDrag(Rigidbody rb, float drag)
        {
#if UNITY_6000_0_OR_NEWER
            rb.angularDamping = drag;
#else
            rb.angularDrag = drag;
#endif
        }
    }
}
