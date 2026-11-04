// ------------------------------------------------
//                 Emotion Effects
//                       by 
//                  Lara Brothers
//           (Roman Lara & Humberto Lara)
// ------------------------------------------------

using UnityEngine;
using System.Collections;

[AddComponentMenu("Lara Brothers/Emotion Effects/Emotion")]

/// <summary>
/// It controls the emotions.
/// </summary>
public class Emotion : MonoBehaviour {
	#region Component Properties

	/// <summary>
	/// To locate the emotions prefabs.
	/// </summary>
	public GameObject Target;

	/// <summary>
	/// The emotions prefabs are saved to be shown.
	/// </summary>
	public GameObject[] Emotions;

	/// <summary>
	/// The emotions positions.
	/// </summary>
	public Vector3[] Positions;

	#endregion

	#region Setters & Getters

	/// <summary>
	/// Gets the length of the emotions.
	/// </summary>
	/// <value>The length of the emotions.</value>
	public int EmotionsLength {
		get { return Emotions.Length; }
	}

	/// <summary>
	/// Gets the length of the positions.
	/// </summary>
	/// <value>The length of the positions.</value>
	public int PositionsLength {
		get { return Positions.Length; }
	}

	#endregion

	#region Unity Methods

	/// <summary>
	/// Awake this component.
	/// </summary>
	public void Awake() {
		if (Emotions == null)
			LogWarningMessage("The Emotions property is empty.");
		if (Positions == null)
			LogWarningMessage("The Positions property is empty.");
		if (EmotionsLength != PositionsLength)
			LogWarningMessage("The Length of the properties Emotions and Positions are not equals.");
	}

	/// <summary>
	/// Start this component.
	/// </summary>
	public void Start() {
		if (Target == null) {
			if (transform.Find("Emotions"))
				Target = transform.Find("Emotions").gameObject;
			else
				LogWarningMessage("The Target property is not established.");
		}
	}

	#endregion

	#region Public Methods

	/// <summary>
	/// Adds the emotion.
	/// </summary>
	/// <param name="i">The index.</param>
	public void AddEmotion(int i) {
		// It clones the emotion.
		GameObject newEmotion = (GameObject) Instantiate(Emotions[i], new Vector3(0, 0, 0), Emotions[i].transform.localRotation);
		newEmotion.transform.parent = Target.transform;
		newEmotion.transform.localPosition = Positions[i];
		newEmotion.transform.name = Emotions[i].name;
	}

	/// <summary>
	/// Removes the emotion.
	/// </summary>
	/// <param name="i">The index.</param>
	public void RemoveEmotion(int i) {
		if (Target.transform.Find(Emotions[i].name))
			Destroy(Target.transform.Find(Emotions[i].name).gameObject);
	}

	/// <summary>
	/// Name the specified i.
	/// </summary>
	/// <param name="i">The index.</param>
	public string Name(int i) {
		return Emotions[i].name;
	}

	#endregion

	#region Private Methods

	/// <summary>
	/// Logs the error message.
	/// </summary>
	/// <param name="msg">Message.</param>
	private void LogErrorMessage(string msg) {
		Debug.LogError("Emotion: " + msg);
	}

	/// <summary>
	/// Logs the warning message.
	/// </summary>
	/// <param name="msg">Message.</param>
	private void LogWarningMessage(string msg) {
		Debug.LogWarning("Emotion: " + msg);
	}

	#endregion
}
