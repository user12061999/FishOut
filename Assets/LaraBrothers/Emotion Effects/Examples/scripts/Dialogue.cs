// ------------------------------------------------
//                 Emotion Effects
//                       by 
//                  Lara Brothers
//           (Roman Lara & Humberto Lara)
// ------------------------------------------------

using UnityEngine;
using System.Collections;

/// <summary>
/// It removes the dialogue.
/// </summary>
public class Dialogue : MonoBehaviour {

	#region Public Methods

	/// <summary>
	/// Removes the dialogue.
	/// </summary>
	public void RemoveDialogue() {
		Destroy(gameObject);
	}

	#endregion
}
