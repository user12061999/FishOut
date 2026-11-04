// ------------------------------------------------
//                 Emotion Effects
//                       by 
//                  Lara Brothers
//           (Roman Lara & Humberto Lara)
// ------------------------------------------------

using UnityEngine;
using System.Collections;

[AddComponentMenu("Lara Brothers/Emotion Effects/Ageing")]
[RequireComponent(typeof(ParticleSystem))]

/// <summary>
/// This component kill a Emotion Particle,
/// at the end of its lifetime.
/// </summary>
public class AgeingEmofx : MonoBehaviour {
	#region Component Properties.

	/// <summary>
	/// Only for its death, namely, 
	/// allow destruct the particle.
	/// </summary>
	public bool DieOfEld;

	#endregion

	#region Unity Methods

	public void LateUpdate() {
		// Check if is alive the particle.
		if (!GetComponent<ParticleSystem>().IsAlive()) {
			// If DieOfEld is false, then only deactivate,
			// otherwise, destruct the particle.
			if (!DieOfEld)
				gameObject.SetActive(false);
			else
				Destroy(gameObject);
		}
	}

	#endregion
}
