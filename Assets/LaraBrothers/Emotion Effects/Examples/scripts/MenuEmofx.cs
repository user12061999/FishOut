// ------------------------------------------------
//                 Emotion Effects
//                       by 
//                  Lara Brothers
//           (Roman Lara & Humberto Lara)
// ------------------------------------------------

using UnityEngine;
using System.Collections;

/// <summary>
/// Menu and controls the show of the emotions and animations.
/// </summary>
public class MenuEmofx : MonoBehaviour {
	#region Component Properties

	/// <summary>
	/// The character.
	/// </summary>
	public GameObject Character;

	/// <summary>
	/// The dialogue.
	/// </summary>
	public GameObject Dialogue;

	#endregion

	#region Class Properties

	/// <summary>
	/// The animation controller.
	/// </summary>
	private Animator _animator;

	/// <summary>
	/// The Emotion script.
	/// </summary>
	private Emotion _emotion;

	/// <summary>
	/// To begin with the show.
	/// </summary>
	private bool _start;

	/// <summary>
	/// To control the show of the emotions and animations.
	/// </summary>
	private int _index;

	/// <summary>
	/// To create a new dilague.
	/// </summary>
	private GameObject _newDialogue;

	#endregion

	#region Unity Methods

	/// <summary>
	/// Start this instance.
	/// </summary>
	void Start() {
		_animator = Character.GetComponent<Animator>();
		_emotion = Character.GetComponent<Emotion>();
		_start = false;
		_index = -1;
	}
	
	/// <summary>
	/// Raises the GUI event.
	/// </summary>
	public void OnGUI() {
		// If the walk is finished, then shows dialogue and Menu.
		if (Character.GetComponent<Walk>().Finish) {
			// If _start is false and is not exist the dialogue, then creates the dialogue.
			if (!_start && !Character.transform.Find("Dialogues").Find(Dialogue.name)) {
				_newDialogue = (GameObject) Instantiate(Dialogue, Dialogue.transform.localPosition, Dialogue.transform.localRotation);
				_newDialogue.transform.parent = Character.transform.Find("Dialogues").transform;
				_newDialogue.transform.name = Dialogue.name;
			}

			// It shows the Menu.
			int hsize = 200;
			int vsize = 80;
			int hloc = Mathf.RoundToInt((Screen.width * 0.5f) - (hsize * 0.5f));
			int vloc = Mathf.RoundToInt((Screen.height - (vsize + (vsize * 0.5f))));
			
			GUI.BeginGroup(new Rect(hloc, vloc, hsize, vsize));
			GUI.Box(new Rect(0, 0, hsize, vsize), "Emotions");
			
			int hsizeLabel = hsize - 20;
			int vsizeLabel = 20;
			int hlocLabel = Mathf.RoundToInt((hsize * 0.5f) - (hsizeLabel * 0.5f));
			int vlocLabel = Mathf.RoundToInt(vsizeLabel);
			
			if (_start) // Only if the user has pressed the Start button, it shows the emotion name current.
				GUI.Label(new Rect(hlocLabel, vlocLabel, hsizeLabel, vsizeLabel), _emotion.Name(_index));
			
			int hsizeButton = 32;
			int vsizeButton = 32;
			int hlocButton = Mathf.RoundToInt(0);
			int vlocButton = Mathf.RoundToInt(0);
			
			int hsizeButtonGroup = Mathf.RoundToInt(hsizeButton * 3);
			int vsizeButtonGroup = Mathf.RoundToInt(vsizeButton);
			int hlocButtonGroup = Mathf.RoundToInt((hsize * 0.5f) - (hsizeButtonGroup * 0.5f));
			int vlocButtonGroup = Mathf.RoundToInt(vsizeLabel * 2);
			
			GUI.BeginGroup(new Rect(hlocButtonGroup, vlocButtonGroup, hsizeButtonGroup, vsizeButtonGroup));

			// If the user has pressed the Start button, then executes the animation and emotion corresponding.
			if (!_start) {
				if (GUI.Button(new Rect(hlocButton, vlocButton, Mathf.RoundToInt(hsizeButton * 3), vsizeButton), "Start")) {
					_start = true;
					++_index;

					if (_newDialogue != null)
						_newDialogue.GetComponent<Animator>().SetBool("Hide", true);
					
					_animator.SetBool("Anim" + (_index), true);
					_emotion.AddEmotion(_index);
				}
			}

			// Once that the user has pressed the Start button, the Menu will keep visible.
			if (_start) {
				// Previous Button
				if (GUI.Button(new Rect(hlocButton, vlocButton, hsizeButton, vsizeButton), "<")) {
					_index = (_index > 0) ? _index - 1 : _emotion.EmotionsLength - 1;
					int prev = ((_index == _emotion.EmotionsLength - 1) ? 0 : _index + 1);
					
					_animator.SetBool("Anim" + prev, false);
					_animator.SetBool("Anim" + _index, true);
					
					_emotion.RemoveEmotion(prev);
					_emotion.AddEmotion(_index);
				}
				
				// Next Button
				if (GUI.Button(new Rect(Mathf.RoundToInt(hsizeButton * 2), vlocButton, hsizeButton, vsizeButton), ">")) {
					_index = (_index < _emotion.EmotionsLength - 1) ? _index + 1: 0;
					int prev = ((_index == 0) ? _emotion.EmotionsLength - 1 : _index - 1);
					
					_animator.SetBool("Anim" + prev, false);
					_animator.SetBool("Anim" + _index, true);
					
					_emotion.RemoveEmotion(prev);
					_emotion.AddEmotion(_index);
				}
			}
			
			GUI.EndGroup();
			GUI.EndGroup();
		}
	}

	#endregion
}
