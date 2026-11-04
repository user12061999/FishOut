// ------------------------------------------------
//                 Emotion Effects
//                       by 
//                  Lara Brothers
//           (Roman Lara & Humberto Lara)
// ------------------------------------------------

using UnityEngine;
using System.Collections;

/// <summary>
/// It makes walking Moti Example Character.
/// </summary>
public class Walk : MonoBehaviour {
	#region Component Properties

	/// <summary>
	/// The end point, where it has to finish.
	/// </summary>
	public Transform EndPoint;

	/// <summary>
	/// The duration of the walk.
	/// </summary>
	public float Duration = 11;

	#endregion

	#region Class Properties

	/// <summary>
	/// The start point, it is the current.
	/// </summary>
	private Vector3 _startPoint;

	/// <summary>
	/// The start time, to determine the time with respect to duration.
	/// </summary>
	private float _startTime;

	/// <summary>
	/// To detect the finish of the travel.
	/// </summary>
	private bool _finish = false;

	#endregion

	#region Getters & Setters

	/// <summary>
	/// Gets a value indicating whether this <see cref="Walk"/> is the finish of the travel.
	/// </summary>
	/// <value><c>true</c> if the travel is finish; otherwise, <c>false</c>.</value>
	public bool Finish {
		get { return _finish; }
	}

	#endregion

	#region Unity Methods

	/// <summary>
	/// Start this component.
	/// </summary>
	public void Start() {
		_startPoint = transform.position;
		_startTime = Time.time;

		// Starts the walk animation.
		GetComponent<Animator>().SetBool("Walk", true);
	}

	/// <summary>
	/// Update this component.
	/// </summary>
	public void Update() {
		if (!Finish) {
			// Per each update, it moves the character.
			transform.position = Vector3.Lerp(_startPoint, EndPoint.localPosition, (Time.time - _startTime) / Duration);
			// If character gets at the same position of EndPoint, it stops the animation.
			if (transform.position.x <= EndPoint.localPosition.x) {
				GetComponent<Animator>().SetBool("Walk", false);
				_finish = true;
			}
		}
	}

	#endregion
}
