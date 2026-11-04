using System;
using System.Collections;
using UnityEngine;

namespace FishOut
{
    public sealed class WaypointMover : MonoBehaviour
    {
        [Min(.1f)] public float maximumSpeed = 12f;
        [Min(.1f)] public float acceleration = 48f;
        public bool IsMoving { get; private set; }
        public bool orientAlongPath;

        public IEnumerator FollowPath(MovementPath path, bool stopAtEnd, Action<Vector3> onStep = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (IsMoving) yield break;
            IsMoving = true;
            try
            {
                float distance = 0, speed = 0;
                float limit = Mathf.Max(.1f, maximumSpeed), ramp = Mathf.Max(.1f, acceleration);
                transform.position = path.Sample(0);
                while (distance < path.Length)
                {
                    float remaining = path.Length - distance;
                    // Brake only at a blocker, never at intermediate waypoints or the escape edge.
                    float desired = stopAtEnd ? Mathf.Min(limit, Mathf.Sqrt(2 * ramp * remaining)) : limit;
                    speed = Mathf.MoveTowards(speed, desired, ramp * Time.deltaTime);
                    distance = Mathf.Min(path.Length, distance + speed * Time.deltaTime);
                    transform.position = path.Sample(distance);
                    if(orientAlongPath)
                    {
                        var tangent=path.Sample(Mathf.Min(path.Length,distance+.18f))-path.Sample(Mathf.Max(0,distance-.05f));
                        if(tangent.sqrMagnitude>.00001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(tangent),600*Time.deltaTime);
                    }
                    onStep?.Invoke(transform.position);
                    yield return null;
                }
                transform.position = path.End;
            }
            finally { IsMoving = false; }
        }
    }
}
