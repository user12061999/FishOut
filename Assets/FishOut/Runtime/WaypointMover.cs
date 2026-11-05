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
        public float startSpeed;
        public bool orientAlongPath;
        // Distance along the path where it joins the shared road; set for fish heading to the gate.
        public float roadStart=float.PositiveInfinity;
        public bool Held { get; set; }
        public float HoldSpeed { get; set; }
        public float Speed { get; private set; }
        public bool OnRoad => IsMoving && travelled>=roadStart;
        public float Remaining { get; private set; }
        private const float QueueBrake = 3;
        private float travelled;
        private MovementPath current;
        public Vector3 PointAhead(float distance)=>current==null?transform.position:current.Sample(travelled+distance);

        public IEnumerator FollowPath(MovementPath path, bool stopAtEnd, Action<Vector3> onStep = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (IsMoving) yield break;
            IsMoving = true;
            try
            {
                float distance = 0, speed = startSpeed;
                travelled=0;Held=false;Remaining=path.Length;current=path;
                float limit = Mathf.Max(.1f, maximumSpeed), ramp = Mathf.Max(.1f, acceleration);
                transform.position = path.Sample(0);
                while (distance < path.Length)
                {
                    float remaining = path.Length - distance;
                    // Brake only at a blocker, never at intermediate waypoints or the escape edge.
                    float desired = stopAtEnd ? Mathf.Min(limit, Mathf.Sqrt(2 * ramp * remaining)) : limit;
                    // A queueing fish eases down to the speed of the one ahead, and stops dead only on contact.
                    if (!Held) speed = Mathf.MoveTowards(speed, desired, ramp * Time.deltaTime);
                    else speed = HoldSpeed <= 0 ? 0 : Mathf.MoveTowards(speed, Mathf.Min(desired, HoldSpeed), ramp * QueueBrake * Time.deltaTime);
                    Speed = speed;
                    distance = Mathf.Min(path.Length, distance + speed * Time.deltaTime);
                    travelled=distance;Remaining=path.Length-distance;
                    transform.position = path.Sample(distance);
                    if(orientAlongPath)
                    {
                        var tangent=path.Sample(Mathf.Min(path.Length,distance+.18f))-path.Sample(Mathf.Max(0,distance-.05f));
                        if(tangent.sqrMagnitude>.00001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(tangent),1200*Time.deltaTime);
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
